using ExcelDataReader;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;

namespace TKWF.Tools.ExcelTools
{
    /// <summary>
    /// 导入失败明细
    /// </summary>
    public class ExcelImportFailure
    {
        public int RowIndex { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, object?> RowValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 导入结果（包含成功项与失败明细）
    /// </summary>
    public class ExcelImportResult<T>
    {
        public List<T> Items { get; } = new();
        public List<ExcelImportFailure> Failures { get; } = new();
        public int SuccessCount => Items.Count;
        public int FailedCount => Failures.Count;
    }

    /// <summary>
    /// Excel 导入工具（保留兼容方法并提供增强方法）
    /// 主要特性：
    /// - 保留兼容的 ImportDataFromExcel / ImportDynamicObjectFromExcel 签名
    /// - 增强版本返回 ExcelImportResult<T> / ExcelImportResult<dynamic>
    /// - 流式读取（ExcelDataReader）、属性 setter 编译缓存、批量属性/动态属性、回调、可配置遇错策略
    /// </summary>
    public static class ExcelTools
    {
        #region 兼容原始签名（保持行为一致）

        /// <summary>
        /// 兼容原始签名：返回 IEnumerable<T>（内部调用增强版并返回 Items）
        /// </summary>
        public static async Task<IEnumerable<T>> ImportDataFromExcel<T>(
            string filename,
            StringDictionary columnMapping,
            string? otherColumnsMappingName = null,
            int sheetIndex = 0)
            where T : new()
        {
            var result = await ImportDataFromExcelWithResult<T>(
                filename,
                columnMapping,
                otherColumnsMappingName,
                sheetIndex,
                batchProperties: null,
                dynamicBatchProperties: null,
                batchOverridesExcel: false,
                onRecordCreated: null,
                log: null,
                stopOnFirstError: false);
            return result.Items;
        }

        /// <summary>
        /// 兼容原始 dynamic 导入方法（保留 AsDataSet 行为以兼容旧调用）
        /// </summary>
        public static async Task<IEnumerable<dynamic>> ImportDynamicObjectFromExcel(
            string filename,
            StringDictionary? columnMapping = null,
            string? otherColumnsMappingName = null,
            int sheetIndex = 0)
        {
            // 使用流式实现替代原 DataTable 实现以提高性能（保持行为兼容）
            var res = await ImportDynamicObjectFromExcelWithResult(
                filename,
                columnMapping,
                otherColumnsMappingName,
                sheetIndex,
                stopOnFirstError: false,
                log: null,
                batchProperties: null,
                dynamicBatchProperties: null,
                batchOverridesExcel: false,
                onRecordCreated: null);

            return res.Items;
        }

        #endregion

        #region 增强版：泛型导入并返回结果

        /// <summary>
        /// 增强版：导入并返回 ExcelImportResult<T>（包含成功列表与失败明细）
        /// 参数说明见方法注释（支持批量属性、动态属性、回调、日志、stopOnFirstError）
        /// </summary>
        public static async Task<ExcelImportResult<T>> ImportDataFromExcelWithResult<T>(
            string filename,
            StringDictionary columnMapping,
            string? otherColumnsMappingName = null,
            int sheetIndex = 0,
            Dictionary<string, object>? batchProperties = null,
            IDictionary<string, Func<int, object>>? dynamicBatchProperties = null,
            bool batchOverridesExcel = false,
            Action<T, int>? onRecordCreated = null,
            Action<string>? log = null,
            bool stopOnFirstError = false)
            where T : new()
        {
            // 参数校验
            columnMapping.AssertNotNull();
            filename.EnsureHasValue().TrimSelf();
            if (!File.Exists(filename))
                throw new FileNotFoundException($"文件不存在：{filename}");

            if (sheetIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex), "sheetIndex 不能为负数");

            // 转成字典，去掉空项
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in columnMapping.Keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var val = columnMapping[key];
                if (string.IsNullOrWhiteSpace(val)) continue;
                mapping[key.Trim()] = val.Trim();
            }
            if (mapping.Count == 0)
                throw new ArgumentException("columnMapping 中没有有效的映射项", nameof(columnMapping));

            // other columns property
            PropertyInfo? otherColumnProperty = null;
            if (!string.IsNullOrEmpty(otherColumnsMappingName))
            {
                otherColumnProperty = typeof(T).GetProperty(otherColumnsMappingName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (otherColumnProperty == null)
                    log?.Invoke($"警告：类型 {typeof(T).FullName} 不存在属性 {otherColumnsMappingName}");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var importResult = new ExcelImportResult<T>();

            await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            IExcelDataReader reader = null!;
            try
            {
                reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                log?.Invoke($"已打开文件：{filename}，开始读取。");

                var currentSheet = 0;
                var processed = false;
                do
                {
                    if (currentSheet == sheetIndex)
                    {
                        processed = true;
                        ProcessSheetStreaming(reader, mapping, otherColumnProperty, batchProperties, dynamicBatchProperties,
                            batchOverridesExcel, onRecordCreated, log, importResult, stopOnFirstError);
                        break;
                    }
                    currentSheet++;
                } while (reader.NextResult());

                if (!processed)
                {
                    log?.Invoke($"指定 sheetIndex={sheetIndex} 超出范围，回退到第一个 Sheet 读取。");
                    reader.Close();
                    stream.Seek(0, SeekOrigin.Begin);
                    reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        ? ExcelReaderFactory.CreateCsvReader(stream)
                        : ExcelReaderFactory.CreateReader(stream);

                    ProcessSheetStreaming(reader, mapping, otherColumnProperty, batchProperties, dynamicBatchProperties,
                        batchOverridesExcel, onRecordCreated, log, importResult, stopOnFirstError);
                }

                log?.Invoke($"读取完成，共导入成功：{importResult.SuccessCount}，失败：{importResult.FailedCount}");
                return importResult;
            }
            catch (Exception ex)
            {
                var msg = $"从 Excel 导入时发生错误：{ex.Message}";
                log?.Invoke(msg);
                throw new InvalidOperationException(msg, ex);
            }
            finally
            {
                try
                {
                    reader?.Close();
                    reader?.Dispose();
                }
                catch { /* 忽略 */ }
            }
        }

        #endregion

        #region 增强版：动态导入并返回结果

        /// <summary>
        /// 动态对象导入（增强版），返回 ExcelImportResult<dynamic>（包含成功项与失败明细）
        /// 支持：stopOnFirstError、batchProperties、dynamicBatchProperties、batchOverridesExcel、onRecordCreated、log
        /// </summary>
        public static async Task<ExcelImportResult<dynamic>> ImportDynamicObjectFromExcelWithResult(
            string filename,
            StringDictionary? columnMapping = null,
            string? otherColumnsMappingName = null,
            int sheetIndex = 0,
            bool stopOnFirstError = false,
            Action<string>? log = null,
            IDictionary<string, object?>? batchProperties = null,
            IDictionary<string, Func<int, object?>>? dynamicBatchProperties = null,
            bool batchOverridesExcel = false,
            Action<dynamic, int>? onRecordCreated = null)
        {
            filename.EnsureHasValue().TrimSelf();
            if (!File.Exists(filename))
                throw new FileNotFoundException($"文件不存在：{filename}");

            if (sheetIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex), "sheetIndex 不能为负数");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var result = new ExcelImportResult<dynamic>();

            await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? ExcelReaderFactory.CreateCsvReader(stream)
                : ExcelReaderFactory.CreateReader(stream);

            try
            {
                var currentSheet = 0;
                var processed = false;

                do
                {
                    if (currentSheet == sheetIndex)
                    {
                        processed = true;

                        if (!reader.Read())
                        {
                            log?.Invoke("Sheet 为空或无可读行，直接返回。");
                            return result;
                        }

                        var fieldCount = reader.FieldCount;
                        var headers = new string[fieldCount];
                        for (var i = 0; i < fieldCount; i++)
                        {
                            var raw = reader.GetValue(i);
                            headers[i] = raw.ToString()!.Trim();
                        }

                        var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < headers.Length; i++)
                        {
                            var h = headers[i];
                            headerToIndex.TryAdd(h, i);
                        }

                        // 构建映射：PropertyName -> columnIndex（当 columnMapping 为 null 时，按 otherColumnsMappingName 逻辑决定）
                        var columnIndexMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        if (columnMapping != null)
                        {
                            foreach (string excelCol in columnMapping.Keys)
                            {
                                if (string.IsNullOrWhiteSpace(excelCol)) continue;
                                var propName = columnMapping[excelCol];
                                if (string.IsNullOrWhiteSpace(propName)) continue;

                                if (headerToIndex.TryGetValue(excelCol, out var idx))
                                    columnIndexMapping[propName] = idx;
                                else
                                {
                                    var found = headerToIndex.Keys.FirstOrDefault(k => string.Equals(k.Trim(), excelCol.Trim(), StringComparison.OrdinalIgnoreCase));
                                    if (found != null)
                                        columnIndexMapping[propName] = headerToIndex[found];
                                    else
                                        log?.Invoke($"列映射警告：未找到 Excel 列 \"{excelCol}\" 映射到属性 {propName}");
                                }
                            }
                        }
                        else
                        {
                            // 没有 columnMapping：若 otherColumnsMappingName 非空，则使用列名作为属性名进行映射（与旧逻辑兼容）
                            if (!string.IsNullOrEmpty(otherColumnsMappingName))
                            {
                                for (var i = 0; i < headers.Length; i++)
                                {
                                    var prop = headers[i];
                                    if (string.IsNullOrWhiteSpace(prop)) continue;
                                    columnIndexMapping[prop] = i;
                                }
                            }
                        }

                        // 逐行读取并构建 ExpandoObject
                        var rowIndex = 0;
                        while (reader.Read())
                        {
                            var rowValues = new object?[fieldCount];
                            for (var i = 0; i < fieldCount; i++)
                            {
                                var v = reader.GetValue(i);
                                rowValues[i] = v == DBNull.Value ? null : v;
                            }

                            var expando = new ExpandoObject() as IDictionary<string, object?>;
                            var failure = new ExcelImportFailure { RowIndex = rowIndex };

                            try
                            {
                                // 1) 如果 batchOverridesExcel == false，先应用批量属性（不会覆盖 Excel 值）
                                if (batchProperties != null && !batchOverridesExcel)
                                {
                                    try
                                    {
                                        ApplyBatchPropertiesToExpando(expando, batchProperties, dynamicBatchProperties, rowIndex);
                                    }
                                    catch (Exception ex)
                                    {
                                        var msg = $"行 {rowIndex} 批量属性设置失败：{ex.Message}";
                                        log?.Invoke(msg);
                                        failure.ErrorMessage ??= string.Empty;
                                        failure.ErrorMessage += msg + "; ";
                                    }
                                }

                                // 2) 映射 Excel 列到 expando（当 columnIndexMapping 有项时）
                                foreach (var kv in columnIndexMapping)
                                {
                                    var propName = kv.Key;
                                    var colIdx = kv.Value;
                                    var val = colIdx >= 0 && colIdx < rowValues.Length ? rowValues[colIdx] : null;
                                    // 对 dynamic 直接赋原始值（保留类型），若需字符串可在调用方或 onRecordCreated 中转换
                                    expando[propName] = val;
                                }

                                // 3) 如果 batchOverridesExcel == true，应用批量属性并覆盖（覆盖 expando 中的同名键）
                                if (batchProperties != null && batchOverridesExcel)
                                {
                                    try
                                    {
                                        ApplyBatchPropertiesToExpando(expando, batchProperties, dynamicBatchProperties, rowIndex);
                                    }
                                    catch (Exception ex)
                                    {
                                        var msg = $"行 {rowIndex} 批量属性设置失败：{ex.Message}";
                                        log?.Invoke(msg);
                                        failure.ErrorMessage ??= string.Empty;
                                        failure.ErrorMessage += msg + "; ";
                                    }
                                }

                                // 4) 构建 otherColumns JSON 并写入 expando（若指定）
                                if (!string.IsNullOrEmpty(otherColumnsMappingName))
                                {
                                    var jsonObj = new JsonObject();
                                    for (var i = 0; i < fieldCount; i++)
                                    {
                                        if (columnIndexMapping.Values.Contains(i)) continue;
                                        var header = headers[i];
                                        var v = rowValues[i];
                                        jsonObj[header] = v?.ToString();
                                    }
                                    expando[otherColumnsMappingName] = jsonObj;
                                }

                                // 5) 回调
                                try
                                {
                                    onRecordCreated?.Invoke(expando, rowIndex);
                                }
                                catch (Exception ex)
                                {
                                    var msg = $"行 {rowIndex} 回调 onRecordCreated 抛异常：{ex.Message}";
                                    log?.Invoke(msg);
                                    failure.ErrorMessage ??= string.Empty;
                                    failure.ErrorMessage += msg + "; ";
                                }

                                // 6) 结果分类
                                if (!string.IsNullOrWhiteSpace(failure.ErrorMessage) || failure.Exception != null)
                                {
                                    // 填充原始列值
                                    for (var i = 0; i < fieldCount; i++)
                                    {
                                        var header = headers[i];
                                        if (!failure.RowValues.ContainsKey(header))
                                            failure.RowValues[header] = rowValues[i];
                                    }

                                    result.Failures.Add(failure);
                                    if (stopOnFirstError)
                                        throw new InvalidOperationException($"在导入时检测到错误（行 {rowIndex}）：{failure.ErrorMessage}", failure.Exception);
                                }
                                else
                                {
                                    result.Items.Add(expando);
                                }
                            }
                            catch (Exception ex)
                            {
                                failure.Exception = ex;
                                failure.ErrorMessage ??= ex.Message;
                                for (var i = 0; i < fieldCount; i++)
                                {
                                    var header = headers[i];
                                    if (!failure.RowValues.ContainsKey(header))
                                        failure.RowValues[header] = rowValues[i];
                                }

                                result.Failures.Add(failure);
                                log?.Invoke($"行 {rowIndex} 处理失败：{ex.Message}");
                                if (stopOnFirstError)
                                    throw new InvalidOperationException($"在导入时检测到错误（行 {rowIndex}）：{ex.Message}", ex);
                            }

                            rowIndex++;
                        }

                        break; // 已处理目标 sheet
                    }

                    currentSheet++;
                } while (reader.NextResult());

                // 若未处理任何 sheet（sheetIndex 超范围），回退第一个 sheet（与旧逻辑一致）
                if (!processed)
                {
                    reader.Close();
                    stream.Seek(0, SeekOrigin.Begin);
                    reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        ? ExcelReaderFactory.CreateCsvReader(stream)
                        : ExcelReaderFactory.CreateReader(stream);

                    // 简单复用上面处理逻辑：读取 header 并处理第一个 sheet
                    if (!reader.Read()) return result;

                    var fieldCount = reader.FieldCount;
                    var headers = new string[fieldCount];
                    for (var i = 0; i < fieldCount; i++)
                    {
                        var raw = reader.GetValue(i);
                        headers[i] = raw.ToString()!.Trim();
                    }

                    var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < headers.Length; i++)
                    {
                        var h = headers[i];
                        headerToIndex.TryAdd(h, i);
                    }

                    var columnIndexMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (columnMapping != null)
                    {
                        foreach (string excelCol in columnMapping.Keys)
                        {
                            if (string.IsNullOrWhiteSpace(excelCol)) continue;
                            var propName = columnMapping[excelCol];
                            if (string.IsNullOrWhiteSpace(propName)) continue;

                            if (headerToIndex.TryGetValue(excelCol, out var idx))
                                columnIndexMapping[propName] = idx;
                            else
                            {
                                var found = headerToIndex.Keys.FirstOrDefault(k => string.Equals(k.Trim(), excelCol.Trim(), StringComparison.OrdinalIgnoreCase));
                                if (found != null)
                                    columnIndexMapping[propName] = headerToIndex[found];
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(otherColumnsMappingName))
                        {
                            for (var i = 0; i < headers.Length; i++)
                            {
                                var prop = headers[i];
                                if (string.IsNullOrWhiteSpace(prop)) continue;
                                columnIndexMapping[prop] = i;
                            }
                        }
                    }

                    var rowIndex = 0;
                    while (reader.Read())
                    {
                        var rowValues = new object?[fieldCount];
                        for (var i = 0; i < fieldCount; i++)
                        {
                            var v = reader.GetValue(i);
                            rowValues[i] = v == DBNull.Value ? null : v;
                        }

                        var expando = new ExpandoObject() as IDictionary<string, object?>;
                        var failure = new ExcelImportFailure { RowIndex = rowIndex };

                        try
                        {
                            if (batchProperties != null && !batchOverridesExcel)
                                ApplyBatchPropertiesToExpando(expando, batchProperties, dynamicBatchProperties, rowIndex);

                            foreach (var kv in columnIndexMapping)
                            {
                                var propName = kv.Key;
                                var colIdx = kv.Value;
                                var val = colIdx >= 0 && colIdx < rowValues.Length ? rowValues[colIdx] : null;
                                expando[propName] = val;
                            }

                            if (batchProperties != null && batchOverridesExcel)
                                ApplyBatchPropertiesToExpando(expando, batchProperties, dynamicBatchProperties, rowIndex);

                            if (!string.IsNullOrEmpty(otherColumnsMappingName))
                            {
                                var jsonObj = new JsonObject();
                                for (var i = 0; i < fieldCount; i++)
                                {
                                    if (columnIndexMapping.Values.Contains(i)) continue;
                                    var header = headers[i];
                                    var v = rowValues[i];
                                    jsonObj[header] = v?.ToString();
                                }
                                expando[otherColumnsMappingName] = jsonObj;
                            }

                            onRecordCreated?.Invoke(expando, rowIndex);

                            result.Items.Add(expando);
                        }
                        catch (Exception ex)
                        {
                            failure.Exception = ex;
                            failure.ErrorMessage ??= ex.Message;
                            for (var i = 0; i < fieldCount; i++)
                            {
                                var header = headers[i];
                                if (!failure.RowValues.ContainsKey(header))
                                    failure.RowValues[header] = rowValues[i];
                            }
                            result.Failures.Add(failure);
                            log?.Invoke($"行 {rowIndex} 处理失败：{ex.Message}");
                            if (stopOnFirstError)
                                throw new InvalidOperationException($"在导入时检测到错误（行 {rowIndex}）：{ex.Message}", ex);
                        }

                        rowIndex++;
                    }
                }

                return result;
            }
            finally
            {
                try { reader?.Close(); reader?.Dispose(); }
                catch
                {
                    // ignored
                }
            }
        }

        #endregion

        #region 内部：流式处理 sheet（泛型实现复用）

        private static void ProcessSheetStreaming<T>(
            IExcelDataReader reader,
            IDictionary<string, string> columnMapping, // ExcelHeader -> PropertyName
            PropertyInfo? otherColumnProperty,
            Dictionary<string, object>? batchProperties,
            IDictionary<string, Func<int, object>>? dynamicBatchProperties,
            bool batchOverridesExcel,
            Action<T, int>? onRecordCreated,
            Action<string>? log,
            ExcelImportResult<T> importResult,
            bool stopOnFirstError)
            where T : new()
        {
            // 读取 header 行（第一行作为 header）
            if (!reader.Read())
            {
                log?.Invoke("Sheet 为空或无可读行，直接返回。");
                return;
            }

            var fieldCount = reader.FieldCount;
            var headers = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                var raw = reader.GetValue(i);
                headers[i] = raw.ToString()!.Trim();
            }

            // header -> index 字典（大小写不敏感）
            var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var h = headers[i];
                headerToIndex.TryAdd(h, i);
            }

            // 将用户提供的 mapping (ExcelHeader -> PropName) 转换为 PropertyName -> columnIndex
            var propertyToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in columnMapping)
            {
                var excelHeader = kv.Key;
                var propName = kv.Value;
                if (string.IsNullOrWhiteSpace(excelHeader) || string.IsNullOrWhiteSpace(propName))
                    continue;

                if (headerToIndex.TryGetValue(excelHeader, out var idx))
                {
                    propertyToIndex[propName] = idx;
                }
                else
                {
                    var found = headerToIndex.Keys.FirstOrDefault(k => string.Equals(k.Trim(), excelHeader.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (found != null && headerToIndex.TryGetValue(found, out idx))
                        propertyToIndex[propName] = idx;
                    else
                        log?.Invoke($"列映射警告：Excel 中未找到列名 \"{excelHeader}\"（映射到属性 {propName}）。");
                }
            }

            // 预建属性缓存与 setter 缓存（提高大量行场景的性能）
            var propertyCache = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            var setterCache = new Dictionary<string, Action<object, object?>>(StringComparer.OrdinalIgnoreCase);

            void EnsurePropertyAndSetter(string propertyName)
            {
                if (string.IsNullOrWhiteSpace(propertyName)) return;
                if (propertyCache.ContainsKey(propertyName)) return;
                var p = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    propertyCache[propertyName] = p;
                    setterCache[propertyName] = CreateSetter(p);
                }
                else
                {
                    log?.Invoke($"警告：类型 {typeof(T).FullName} 不存在属性 {propertyName}");
                }
            }

            foreach (var propName in propertyToIndex.Keys)
                EnsurePropertyAndSetter(propName);

            if (batchProperties != null)
            {
                foreach (var kv in batchProperties.Keys)
                    EnsurePropertyAndSetter(kv);
            }

            if (dynamicBatchProperties != null)
            {
                foreach (var kv in dynamicBatchProperties.Keys)
                    EnsurePropertyAndSetter(kv);
            }

            // 逐行读取数据
            var rowIndex = 0;
            while (reader.Read())
            {
                // 读取整行原始值
                var rowValues = new object?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    var val = reader.GetValue(i);
                    rowValues[i] = val == DBNull.Value ? null : val;
                }

                var record = new T();
                var failure = new ExcelImportFailure { RowIndex = rowIndex };

                try
                {
                    // 1) 如果 batchOverridesExcel == false，则先应用批量属性（不会覆盖 Excel 值）
                    if (batchProperties != null && !batchOverridesExcel)
                    {
                        ApplyBatchProperties(record, batchProperties, dynamicBatchProperties, rowIndex, propertyCache, setterCache, log, ref failure);
                        if (!string.IsNullOrEmpty(failure.ErrorMessage) && stopOnFirstError)
                        {
                            throw new InvalidOperationException($"行 {rowIndex} 批量属性设置失败: {failure.ErrorMessage}");
                        }
                    }

                    // 2) 将 Excel 列映射赋值到对象（若 batchOverridesExcel == true 则先略过，后面再覆盖）
                    foreach (var kv in propertyToIndex)
                    {
                        var propName = kv.Key;
                        var colIdx = kv.Value;
                        if (!propertyCache.TryGetValue(propName, out var propInfo))
                            continue;

                        var setter = setterCache.GetValueOrDefault(propName);
                        var rawValue = colIdx >= 0 && colIdx < rowValues.Length ? rowValues[colIdx] : null;

                        object? converted;
                        try
                        {
                            converted = ConvertToTarget(rawValue, propInfo.PropertyType);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 列索引 {colIdx} 映射到属性 {propName} 时转换失败：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                            failure.RowValues[propName] = rawValue;
                            converted = GetDefaultValue(propInfo.PropertyType);
                        }

                        if (batchOverridesExcel)
                        {
                            // 延迟 Excel 赋值（将在 batch 覆盖后再决定是否覆盖）
                            continue;
                        }
                        else
                        {
                            try
                            {
                                if (setter != null)
                                    setter(record!, converted);
                                else
                                    propInfo.SetValue(record, converted);
                            }
                            catch (Exception ex)
                            {
                                var msg = $"行 {rowIndex} 设置属性 {propName} 失败：{ex.Message}";
                                log?.Invoke(msg);
                                failure.ErrorMessage ??= string.Empty;
                                failure.ErrorMessage += msg + "; ";
                                failure.RowValues[propName] = rawValue;
                            }
                        }
                    }

                    // 3) 如果 batchOverridesExcel == true，则现在应用批量属性并覆盖 Excel 值
                    if (batchProperties != null && batchOverridesExcel)
                    {
                        ApplyBatchProperties(record, batchProperties, dynamicBatchProperties, rowIndex, propertyCache, setterCache, log, ref failure);
                        if (!string.IsNullOrEmpty(failure.ErrorMessage) && stopOnFirstError)
                        {
                            throw new InvalidOperationException($"行 {rowIndex} 批量属性设置失败: {failure.ErrorMessage}");
                        }
                    }

                    // 4) 生成未映射列的 JSON 并写入 otherColumnProperty（若指定）
                    if (otherColumnProperty != null)
                    {
                        var otherDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < fieldCount; i++)
                        {
                            var header = headers[i];
                            var mapped = columnMapping.Any(kv => string.Equals(kv.Key, header, StringComparison.OrdinalIgnoreCase));
                            if (mapped) continue;
                            otherDict[header] = rowValues[i];
                        }

                        try
                        {
                            var json = JsonSerializer.Serialize(otherDict, new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                WriteIndented = true,
                                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                            });

                            otherColumnProperty.SetValue(record, json);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 序列化其他列失败：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                        }
                    }

                    // 5) 回调
                    try
                    {
                        onRecordCreated?.Invoke(record, rowIndex);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"行 {rowIndex} 回调 onRecordCreated 抛异常：{ex.Message}";
                        log?.Invoke(msg);
                        failure.ErrorMessage ??= string.Empty;
                        failure.ErrorMessage += msg + "; ";
                    }

                    // 6) 根据 failure 判断是否记录失败或成功
                    if (!string.IsNullOrWhiteSpace(failure.ErrorMessage) || failure.Exception != null)
                    {
                        // 填充 row 原始值（若尚未填写）
                        for (var i = 0; i < fieldCount; i++)
                        {
                            var header = headers[i];
                            if (!failure.RowValues.ContainsKey(header))
                                failure.RowValues[header] = rowValues[i];
                        }

                        importResult.Failures.Add(failure);

                        if (stopOnFirstError)
                        {
                            throw new InvalidOperationException($"在导入时检测到错误（行 {rowIndex}）：{failure.ErrorMessage}", failure.Exception);
                        }
                    }
                    else
                    {
                        importResult.Items.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    // 捕获整行无法处理的异常
                    failure.Exception = ex;
                    failure.ErrorMessage ??= ex.Message;
                    for (var i = 0; i < fieldCount; i++)
                    {
                        var header = headers[i];
                        if (!failure.RowValues.ContainsKey(header))
                            failure.RowValues[header] = rowValues[i];
                    }

                    importResult.Failures.Add(failure);
                    log?.Invoke($"行 {rowIndex} 处理失败：{ex.Message}");

                    if (stopOnFirstError)
                    {
                        throw new InvalidOperationException($"在导入时检测到错误（行 {rowIndex}）：{ex.Message}", ex);
                    }
                }

                rowIndex++;
            }
        }

        #endregion

        #region 批量属性应用（泛型与 dynamic 辅助）

        private static void ApplyBatchProperties<T>(
            T instance,
            IDictionary<string, object> batchStatic,
            IDictionary<string, Func<int, object>>? batchDynamic,
            int rowIndex,
            Dictionary<string, PropertyInfo> propertyCache,
            Dictionary<string, Action<object, object?>> setterCache,
            Action<string>? log,
            ref ExcelImportFailure failure)
        {
            // 静态属性
            foreach (var kv in batchStatic)
            {
                var propName = kv.Key;
                var rawVal = kv.Value;
                if (!propertyCache.TryGetValue(propName, out var propInfo))
                {
                    propInfo = typeof(T).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (propInfo == null)
                    {
                        var msg = $"批量设置警告：类型 {typeof(T).FullName} 未包含属性 {propName}";
                        log?.Invoke(msg);
                        failure.ErrorMessage ??= string.Empty;
                        failure.ErrorMessage += msg + "; ";
                        continue;
                    }
                    propertyCache[propName] = propInfo;
                }

                var setter = setterCache.GetValueOrDefault(propName);
                object? converted;
                try
                {
                    converted = ConvertToTarget(rawVal, propInfo.PropertyType);
                }
                catch (Exception ex)
                {
                    var msg = $"批量设置属性 {propName} 转换失败：{ex.Message}";
                    log?.Invoke(msg);
                    failure.ErrorMessage ??= string.Empty;
                    failure.ErrorMessage += msg + "; ";
                    converted = GetDefaultValue(propInfo.PropertyType);
                }

                try
                {
                    if (setter != null)
                        setter(instance!, converted);
                    else
                        propInfo.SetValue(instance, converted);
                }
                catch (Exception ex)
                {
                    var msg = $"批量设置属性 {propName} 赋值失败：{ex.Message}";
                    log?.Invoke(msg);
                    failure.ErrorMessage ??= string.Empty;
                    failure.ErrorMessage += msg + "; ";
                }
            }

            // 动态属性
            if (batchDynamic == null) return;

            foreach (var kv in batchDynamic)
            {
                var propName = kv.Key;
                var func = kv.Value;
                object? rawVal;
                try
                {
                    rawVal = func(rowIndex);
                }
                catch (Exception ex)
                {
                    var msg = $"动态批量属性 {propName} 在计算时抛出异常：{ex.Message}";
                    log?.Invoke(msg);
                    failure.ErrorMessage ??= string.Empty;
                    failure.ErrorMessage += msg + "; ";
                    continue;
                }

                if (!propertyCache.TryGetValue(propName, out var propInfo))
                {
                    propInfo = typeof(T).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (propInfo == null)
                    {
                        var msg = $"动态批量设置警告：类型 {typeof(T).FullName} 未包含属性 {propName}";
                        log?.Invoke(msg);
                        failure.ErrorMessage ??= string.Empty;
                        failure.ErrorMessage += msg + "; ";
                        continue;
                    }
                    propertyCache[propName] = propInfo;
                }

                var setter = setterCache.GetValueOrDefault(propName);
                object? converted;
                try
                {
                    converted = ConvertToTarget(rawVal, propInfo.PropertyType);
                }
                catch (Exception ex)
                {
                    var msg = $"动态批量属性 {propName} 转换失败：{ex.Message}";
                    log?.Invoke(msg);
                    failure.ErrorMessage ??= string.Empty;
                    failure.ErrorMessage += msg + "; ";
                    converted = GetDefaultValue(propInfo.PropertyType);
                }

                try
                {
                    if (setter != null)
                        setter(instance!, converted);
                    else
                        propInfo.SetValue(instance, converted);
                }
                catch (Exception ex)
                {
                    var msg = $"动态批量属性 {propName} 赋值失败：{ex.Message}";
                    log?.Invoke(msg);
                    failure.ErrorMessage ??= string.Empty;
                    failure.ErrorMessage += msg + "; ";
                }
            }
        }

        // 将批量属性应用到 ExpandoObject（dynamic 版本，直接赋值）
        private static void ApplyBatchPropertiesToExpando(IDictionary<string, object?> expando, IDictionary<string, object?> batchStatic, IDictionary<string, Func<int, object?>>? batchDynamic, int rowIndex)
        {
            foreach (var kv in batchStatic)
            {
                expando[kv.Key] = kv.Value;
            }

            if (batchDynamic != null)
            {
                foreach (var kv in batchDynamic)
                {
                    expando[kv.Key] = kv.Value(rowIndex);
                }
            }
        }

        #endregion

        #region 类型转换 / 默认值 / 动态 setter 帮助方法

        private static object? ConvertToTarget(object? value, Type targetType)
        {
            if (value == null) return GetDefaultOrNull(targetType);

            if (targetType.IsInstanceOfType(value)) return value;

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is string s && string.IsNullOrWhiteSpace(s))
                return GetDefaultOrNull(targetType);

            if (nonNullable.IsEnum)
            {
                try
                {
                    // 1. 将 value 转为字符串（统一匹配入口，兼容数值/英文名/DisplayName）
                    var input = value.ToString()!.Trim();

                    // 2. 反射调用 EnumHelper.ParseEnum<T> 泛型方法（多维度匹配）
                    // 先获取 EnumHelper 的 ParseEnum 泛型方法（无自定义默认值，使用兜底默认值）
                    var parseEnumMethod = typeof(EnumHelper)
                        .GetMethod(nameof(EnumHelper.ParseEnum), new[] { typeof(string) })!
                        .MakeGenericMethod(nonNullable);

                    // 3. 调用方法并返回枚举值
                    var enumValue = parseEnumMethod.Invoke(null, new object[] { input })!;
                    return enumValue;
                }
                catch (Exception ex)
                {
                    // 转换失败时返回枚举默认值（兜底，避免整行导入失败）
                    return GetDefaultOrNull(targetType);
                }
            }

            if (nonNullable == typeof(Guid))
            {
                if (value is Guid g) return g;
                if (value is string gs) return Guid.Parse(gs);
            }

            if (nonNullable == typeof(DateTime))
            {
                if (value is DateTime dt) return dt;
                if (value is string ds) return DateTime.Parse(ds);
            }

            if (nonNullable == typeof(bool))
            {
                if (value is bool b) return b;
                if (value is string bs) return bool.Parse(bs);
                if (value is int i) return i != 0;
            }

            try
            {
                var converted = Convert.ChangeType(value, nonNullable);
                return converted;
            }
            catch
            {
                if (value is string vs)
                {
                    var ctor = nonNullable.GetConstructor([typeof(string)]);
                    if (ctor != null)
                        return ctor.Invoke([vs]);
                }
                return GetDefaultOrNull(targetType);
            }
        }

        private static object? GetDefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        private static object? GetDefaultOrNull(Type type)
        {
            if (!type.IsValueType) return null;
            if (Nullable.GetUnderlyingType(type) != null) return null;
            return Activator.CreateInstance(type);
        }

        private static Action<object, object?> CreateSetter(PropertyInfo prop)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);

            var convertMethod = typeof(ExcelTools).GetMethod(nameof(ConvertToTarget), BindingFlags.NonPublic | BindingFlags.Static)!;
            var convertCall = Expression.Call(convertMethod, valueParam, Expression.Constant(prop.PropertyType, typeof(Type)));
            var converted = Expression.Convert(convertCall, prop.PropertyType);

            var call = Expression.Call(instanceCast, prop.SetMethod!, converted);
            var lambda = Expression.Lambda<Action<object, object?>>(call, instanceParam, valueParam);
            return lambda.Compile();
        }

        #endregion
    }
}