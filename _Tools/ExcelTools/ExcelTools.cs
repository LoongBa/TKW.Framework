using ExcelDataReader;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;

namespace TKWF.Tools.ExcelTools
{
    /// <summary>
    /// Excel 导入工具（保留兼容方法并提供增强方法）
    /// 主要特性：
    /// - 保留兼容的 ImportDataFromExcel / ImportDynamicObjectFromExcel 签名
    /// - 增强版本返回 ExcelImportResult<T> / ExcelImportResult<dynamic>
    /// - 流式读取（ExcelDataReader）、属性 setter 编译缓存、批量属性/动态属性
    /// - 可控回调（onRecordValidating）：支持忽略/失败/终止流程控制
    /// - 普通回调（onRecordCreated）：通知型回调，用于数据补充/日志记录
    /// - 可配置遇错策略、日志记录
    /// </summary>
    public static class ExcelTools
    {
        #region 开放工具方法（公开可见，供外部调用）
        /// <summary>
        /// 判断指定的 sheetIndex 是否有效（无需遍历所有 Sheet，仅遍历到 sheetIndex 即可）
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetIndex">待校验的 Sheet 索引（从 0 开始）</param>
        /// <returns>true=有效，false=无效</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">sheetIndex 为负数时抛出</exception>
        public static bool IsSheetIndexValid(string filePath, int sheetIndex)
        {
            // 参数校验
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}", filePath);
            switch (sheetIndex)
            {
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(sheetIndex), "Sheet 索引不能为负数");
                // 特殊情况：sheetIndex == 0，直接返回有效（无需遍历）
                case 0:
                    return true;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            IExcelDataReader? reader = null;
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // 创建 Reader
                reader = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                var currentSheetIndex = 0;
                // 仅遍历到 sheetIndex 即可，无需遍历所有 Sheet
                do
                {
                    // 若当前索引已等于目标索引，说明有效
                    if (currentSheetIndex == sheetIndex)
                        return true;

                    currentSheetIndex++;
                    // 若已无下一个 Sheet，终止循环
                } while (reader.NextResult());

                // 循环结束仍未找到，说明索引无效
                return false;
            }
            finally
            {
                // 安全释放 Reader
                if (reader != null)
                {
                    try { reader.Close(); reader.Dispose(); }
                    catch { /* 忽略释放异常 */ }
                }
            }
        }

        /// <summary>
        /// 获取 Excel 文件的 Sheet 总数 + 所有 Sheet 名称（遍历全量，供其他场景复用）
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <returns>键=Sheet索引，值=Sheet名称；字典.Count 即为 Sheet 总数</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static Dictionary<int, string> GetExcelSheetInfo(string filePath)
        {
            // 参数校验
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在：{filePath}", filePath);

            var sheetInfoDict = new Dictionary<int, string>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            IExcelDataReader? reader = null;

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // 创建 Reader
                reader = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                var currentSheetIndex = 0;
                // 遍历所有 Sheet，收集索引和名称
                do
                {
                    sheetInfoDict.Add(currentSheetIndex, reader.Name);
                    currentSheetIndex++;
                } while (reader.NextResult());

                return sheetInfoDict;
            }
            finally
            {
                // 安全释放 Reader
                if (reader != null)
                {
                    try { reader.Close(); reader.Dispose(); }
                    catch { /* 忽略释放异常 */ }
                }
            }
        }

        /// <summary>
        /// 重载：获取 Excel 文件的 Sheet 总数（基于 GetExcelSheetInfo）
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <returns>Sheet 总数</returns>
        public static int GetExcelSheetCount(string filePath)
        {
            return GetExcelSheetInfo(filePath).Count;
        }

        /// <summary>
        /// 类型转换工具：将原始值转换为目标类型（公开方法，供外部复用）
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换后的值</returns>
        public static object? ConvertToTarget(object? value, Type targetType)
        {
            if (value == null) return GetDefaultOrNull(targetType);

            if (targetType.IsInstanceOfType(value)) return value;

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is string s && string.IsNullOrWhiteSpace(s))
                return GetDefaultOrNull(targetType);

            // 枚举类型转换
            if (nonNullable.IsEnum)
            {
                try
                {
                    var input = value.ToString()!.Trim();
                    var parseEnumMethod = typeof(EnumHelper)
                        .GetMethod(nameof(EnumHelper.ParseEnum), [typeof(string)])!
                        .MakeGenericMethod(nonNullable);
                    var enumValue = parseEnumMethod.Invoke(null, [input])!;
                    return enumValue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"枚举转换失败：{ex.Message}，返回默认值");
                    return GetDefaultOrNull(targetType);
                }
            }

            // Guid类型转换
            if (nonNullable == typeof(Guid))
            {
                if (value is Guid g) return g;
                if (value is string gs && Guid.TryParse(gs, out var guid)) return guid;
            }

            // DateTime类型转换
            if (nonNullable == typeof(DateTime))
            {
                if (value is DateTime dt) return dt;
                if (value is string ds && DateTime.TryParse(ds, out var date)) return date;
            }

            // Boolean类型转换
            if (nonNullable == typeof(bool))
            {
                if (value is bool b) return b;
                if (value is string bs && bool.TryParse(bs, out var flag)) return flag;
                if (value is int i) return i != 0;
            }

            // 通用类型转换
            try
            {
                var converted = Convert.ChangeType(value, nonNullable);
                return converted;
            }
            catch
            {
                // 尝试通过构造函数转换
                if (value is string vs)
                {
                    var ctor = nonNullable.GetConstructor([typeof(string)]);
                    if (ctor != null)
                        return ctor.Invoke([vs]);
                }
                return GetDefaultOrNull(targetType);
            }
        }

        /// <summary>
        /// 获取类型默认值（公开方法，供外部复用）
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>类型默认值</returns>
        public static object? GetDefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        /// <summary>
        /// 获取可空类型/值类型的默认值（公开方法，供外部复用）
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>默认值（null或类型默认值）</returns>
        public static object? GetDefaultOrNull(Type type)
        {
            if (!type.IsValueType) return null;
            if (Nullable.GetUnderlyingType(type) != null) return null;
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 给动态对象（ExpandoObject）应用批量属性（公开方法，供外部复用）
        /// </summary>
        /// <param name="expando">动态对象</param>
        /// <param name="batchStatic">固定批量属性</param>
        /// <param name="batchDynamic">动态批量属性</param>
        /// <param name="rowIndex">当前行索引</param>
        public static void ApplyBatchPropertiesToExpando(
            IDictionary<string, object?> expando,
            IDictionary<string, object?> batchStatic,
            IDictionary<string, Func<int, object?>>? batchDynamic,
            int rowIndex)
        {
            // 应用固定属性
            foreach (var kv in batchStatic)
            {
                expando[kv.Key] = kv.Value;
            }

            // 应用动态属性
            if (batchDynamic != null)
            {
                foreach (var kv in batchDynamic)
                {
                    expando[kv.Key] = kv.Value(rowIndex);
                }
            }
        }
        #endregion

        #region 兼容原始签名（保持行为一致，内部调用增强版）
        /// <summary>
        /// 兼容原始签名：返回 IEnumerable<T>（内部调用增强版并返回 Items）
        /// </summary>
        /// <typeparam name="T">目标实体类型</typeparam>
        /// <param name="filename">Excel文件路径</param>
        /// <param name="columnMapping">列映射关系（Excel列名->实体属性名）</param>
        /// <param name="otherColumnsMappingName">其他未映射列的属性名（存储JSON字符串）</param>
        /// <param name="sheetIndex">工作表索引（从0开始）</param>
        /// <returns>导入的实体列表</returns>
        public static async Task<IEnumerable<T>> ImportDataFromExcel<T>(
            string filename,
            Dictionary<string, string> columnMapping,
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
                stopOnFirstError: false,
                onRecordValidating: null);
            return result.Items;
        }

        /// <summary>
        /// 兼容原始 dynamic 导入方法（保留 AsDataSet 行为以兼容旧调用）
        /// </summary>
        /// <param name="filename">Excel文件路径</param>
        /// <param name="columnMapping">列映射关系（Excel列名->动态对象属性名）</param>
        /// <param name="otherColumnsMappingName">其他未映射列的属性名（存储JSON对象）</param>
        /// <param name="sheetIndex">工作表索引（从0开始）</param>
        /// <returns>导入的动态对象列表</returns>
        public static async Task<IEnumerable<dynamic>> ImportDynamicObjectFromExcel(
            string filename,
            Dictionary<string, string> columnMapping,
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
                onRecordCreated: null,
                onRecordValidating: null);

            return res.Items;
        }
        #endregion

        #region 增强版：泛型导入（强类型）- 统一逻辑，修复冗余
        /// <summary>
        /// 增强版：导入并返回 ExcelImportResult<T>（包含成功列表与失败明细）
        /// 支持批量属性、动态属性、可控流程回调、普通通知回调、日志记录、遇错策略
        /// </summary>
        /// <typeparam name="T">目标实体类型</typeparam>
        /// <param name="filename">Excel 文件路径</param>
        /// <param name="columnMapping">列映射关系（Excel列名->实体属性名）</param>
        /// <param name="otherColumnsMappingName">其他未映射列的属性名（存储JSON字符串）</param>
        /// <param name="sheetIndex">工作表索引（从0开始）</param>
        /// <param name="batchProperties">批量设置的固定属性（属性名->固定值）</param>
        /// <param name="dynamicBatchProperties">动态批量属性（属性名->行索引关联的取值方法）</param>
        /// <param name="batchOverridesExcel">批量属性是否覆盖Excel中的同名属性值</param>
        /// <param name="onRecordCreated">普通通知回调（记录创建后触发，无流程控制能力）</param>
        /// <param name="log">日志记录委托（用于输出导入过程信息）</param>
        /// <param name="stopOnFirstError">遇到第一个错误时是否停止导入</param>
        /// <param name="onRecordValidating">可控验证回调（用于业务校验，支持控制流程走向）</param>
        /// <returns>Excel导入结果（含成功数据与失败明细）</returns>
        public static async Task<ExcelImportResult<T>> ImportDataFromExcelWithResult<T>(
            string filename,
            Dictionary<string, string> columnMapping,
            string? otherColumnsMappingName = null,
            int sheetIndex = 0,
            Dictionary<string, object>? batchProperties = null,
            IDictionary<string, Func<int, object>>? dynamicBatchProperties = null,
            bool batchOverridesExcel = false,
            Action<int, T, Dictionary<string, object?>>? onRecordCreated = null,
            Action<string>? log = null,
            bool stopOnFirstError = false,
            ExcelRecordValidatingCallback<T>? onRecordValidating = null)
            where T : new()
        {
            // 统一参数校验（与动态类型方法保持一致）
            columnMapping.AssertNotNull();
            filename = filename.EnsureHasValue().TrimSelf();
            if (!File.Exists(filename))
                throw new FileNotFoundException($"文件不存在：{filename}", filename);

            if (sheetIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex), "sheetIndex 不能为负数");

            // Sheet索引有效性校验（与动态类型方法逻辑统一，非0校验，无效抛异常）
            if (sheetIndex != 0)
            {
                var isSheetValid = IsSheetIndexValid(filename, sheetIndex);
                if (!isSheetValid)
                {
                    var sheetCount = GetExcelSheetCount(filename);
                    throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                        $"指定的Sheet索引 {sheetIndex} 无效，该Excel文件共有 {sheetCount} 个Sheet（有效索引范围：0 - {sheetCount - 1}）");
                }
            }

            // 转成字典，去掉空项（大小写不敏感）
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in columnMapping.Keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var val = columnMapping[key];
                if (string.IsNullOrWhiteSpace(val)) continue;
                mapping[key.Trim()] = val.Trim();
            }
            if (mapping.Count == 0)
                throw new ArgumentException("columnMapping 中没有有效的映射项", nameof(columnMapping));

            // 验证其他列属性是否存在
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
            IExcelDataReader? reader = null;

            try
            {
                // 仅创建一次Reader（与动态类型方法一致，避免文件关闭异常）
                reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                log?.Invoke($"已打开文件：{filename}，开始读取目标Sheet（索引：{sheetIndex}）。");

                var currentSheet = 0;
                var processed = false;
                do
                {
                    if (currentSheet == sheetIndex)
                    {
                        processed = true;
                        // 流式处理目标工作表（核心逻辑，提取复用）
                        ProcessSheetStreaming(reader, mapping, otherColumnProperty, batchProperties, dynamicBatchProperties,
                            batchOverridesExcel, onRecordCreated, log, importResult, stopOnFirstError, onRecordValidating);
                        break;
                    }
                    currentSheet++;
                } while (reader.NextResult());

                // 兜底提示（与动态类型方法一致）
                if (!processed)
                {
                    log?.Invoke($"警告：未找到Sheet索引 {sheetIndex}，未处理任何数据");
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
                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                    }
                }
                catch { /* 忽略资源释放异常 */ }
            }
        }
        #endregion

        #region 增强版：动态导入（ExpandoObject）- 修复变量错误，统一逻辑
        /// <summary>
        /// 动态对象导入（增强版），返回 ExcelImportResult<dynamic>（包含成功项与失败明细）
        /// 支持可控流程回调、批量属性、动态属性、遇错策略、日志记录
        /// </summary>
        /// <param name="filename">Excel文件路径</param>
        /// <param name="columnMapping">列映射关系（Excel列名->动态对象属性名）</param>
        /// <param name="otherColumnsMappingName">其他未映射列的属性名（存储JSON对象）</param>
        /// <param name="sheetIndex">工作表索引（从0开始）</param>
        /// <param name="stopOnFirstError">遇到第一个错误时是否停止导入</param>
        /// <param name="log">日志记录委托（用于输出导入过程信息）</param>
        /// <param name="batchProperties">批量设置的固定属性（属性名->固定值）</param>
        /// <param name="dynamicBatchProperties">动态批量属性（属性名->行索引关联的取值方法）</param>
        /// <param name="batchOverridesExcel">批量属性是否覆盖Excel中的同名属性值</param>
        /// <param name="onRecordCreated">普通通知回调（动态对象创建后触发，无流程控制能力）</param>
        /// <param name="onRecordValidating">可控验证回调（用于业务校验，支持控制流程走向）</param>
        /// <returns>Excel导入结果（含成功动态对象与失败明细）</returns>
        public static async Task<ExcelImportResult<dynamic>> ImportDynamicObjectFromExcelWithResult(
                string filename,
                Dictionary<string, string>? columnMapping = null,
                string? otherColumnsMappingName = null,
                int sheetIndex = 0,
                bool stopOnFirstError = false,
                Action<string>? log = null,
                IDictionary<string, object?>? batchProperties = null,
                IDictionary<string, Func<int, object?>>? dynamicBatchProperties = null,
                bool batchOverridesExcel = false,
                Action<dynamic, int>? onRecordCreated = null,
                ExcelDynamicRecordValidatingCallback? onRecordValidating = null)
        {
            // 1. 基础参数校验（与泛型方法逻辑完全统一）
            filename = filename.EnsureHasValue().TrimSelf();
            if (!File.Exists(filename))
                throw new FileNotFoundException($"文件不存在：{filename}", filename);

            if (sheetIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex), "sheetIndex 不能为负数");

            // 2. Sheet 索引有效性校验（与泛型方法完全一致）
            if (sheetIndex != 0)
            {
                var isSheetValid = IsSheetIndexValid(filename, sheetIndex);
                if (!isSheetValid)
                {
                    var sheetCount = GetExcelSheetCount(filename);
                    throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                        $"指定的Sheet索引 {sheetIndex} 无效，该Excel文件共有 {sheetCount} 个Sheet（有效索引范围：0 - {sheetCount - 1}）");
                }
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var result = new ExcelImportResult<dynamic>();

            await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            IExcelDataReader? reader = null;

            try
            {
                // 仅创建一次Reader（与泛型方法一致，避免文件关闭异常）
                reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                log?.Invoke($"已成功打开Excel文件：{filename}，准备读取Sheet索引 {sheetIndex} 的动态对象数据");

                var currentSheet = 0;
                var processed = false;

                do
                {
                    if (currentSheet == sheetIndex)
                    {
                        processed = true;
                        // 修复变量引用错误：移除无效的 rowIndex 参数（原代码误传）
                        ProcessDynamicSheetData(reader, columnMapping, otherColumnsMappingName, stopOnFirstError, log,
                            batchProperties, dynamicBatchProperties, batchOverridesExcel, onRecordCreated, onRecordValidating, result);
                        break; // 已处理目标sheet，跳出循环
                    }

                    currentSheet++;
                } while (reader.NextResult());

                // 兜底提示（与泛型方法一致）
                if (!processed)
                {
                    log?.Invoke($"警告：未找到Sheet索引 {sheetIndex}，未处理任何数据");
                }

                log?.Invoke($"动态对象Excel导入完成！成功导入 {result.SuccessCount} 条数据，失败 {result.FailedCount} 条数据");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"从Excel文件 {filename} 导入动态对象时发生异常：{ex.Message}";
                log?.Invoke(errorMsg);
                throw new InvalidOperationException(errorMsg, ex);
            }
            finally
            {
                // 安全释放Reader资源（与泛型方法一致）
                try { reader?.Close(); reader?.Dispose(); }
                catch { /* 忽略资源释放异常，避免影响主流程 */ }
            }
        }
        #endregion

        #region 内部核心处理方法（泛型+动态，逻辑对齐，提取复用）
        /// <summary>
        /// 流式处理Excel工作表（泛型核心逻辑，强类型专用）
        /// </summary>
        /// <typeparam name="T">目标实体类型</typeparam>
        /// <param name="reader">Excel数据读取器</param>
        /// <param name="columnMapping">列映射关系（Excel列名->实体属性名）</param>
        /// <param name="otherColumnProperty">其他未映射列的属性信息</param>
        /// <param name="batchProperties">批量固定属性</param>
        /// <param name="dynamicBatchProperties">动态批量属性</param>
        /// <param name="batchOverridesExcel">批量属性是否覆盖Excel值</param>
        /// <param name="onRecordCreated">普通通知回调</param>
        /// <param name="log">日志委托</param>
        /// <param name="importResult">导入结果对象</param>
        /// <param name="stopOnFirstError">遇错即停</param>
        /// <param name="onRecordValidating">可控验证回调</param>
        private static void ProcessSheetStreaming<T>(
            IExcelDataReader reader,
            IDictionary<string, string> columnMapping,
            PropertyInfo? otherColumnProperty,
            Dictionary<string, object>? batchProperties,
            IDictionary<string, Func<int, object>>? dynamicBatchProperties,
            bool batchOverridesExcel,
            Action<int, T, Dictionary<string, object?>>? onRecordCreated,
            Action<string>? log,
            ExcelImportResult<T> importResult,
            bool stopOnFirstError,
            ExcelRecordValidatingCallback<T>? onRecordValidating)
            where T : new()
        {
            // 读取header行（第一行作为列名）
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

            // header -> 列索引 字典（大小写不敏感）
            var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var h = headers[i];
                headerToIndex.TryAdd(h, i);
            }

            // 转换映射关系：实体属性名 -> Excel列索引
            var propertyToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in columnMapping)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;

                if (headerToIndex.TryGetValue(key, out var idx))
                    propertyToIndex[value] = idx;
                else
                {
                    var found = headerToIndex.Keys.FirstOrDefault(k => string.Equals(k.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (found != null && headerToIndex.TryGetValue(found, out idx))
                        propertyToIndex[value] = idx;
                    else
                        log?.Invoke($"列映射警告：Excel 中未找到列名 \"{key}\"（映射到属性 {value}）。");
                }
            }

            // 预建属性缓存与setter编译缓存（提升大数据量性能）
            var propertyCache = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            var setterCache = new Dictionary<string, Action<object, object?>>(StringComparer.OrdinalIgnoreCase);

            // 提前缓存所有需要的属性与setter
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
                // 读取当前行原始值
                var rowValues = new object?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    var val = reader.GetValue(i);
                    rowValues[i] = val == DBNull.Value ? null : val;
                }

                var record = new T();
                var failure = new ExcelImportFailure { RowIndex = rowIndex };
                var otherDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    // 1) 先应用批量属性（不覆盖Excel值）
                    if (batchProperties != null && !batchOverridesExcel)
                    {
                        ApplyBatchProperties(record, batchProperties, dynamicBatchProperties, rowIndex, propertyCache, setterCache, log, ref failure);
                        if (!string.IsNullOrEmpty(failure.ErrorMessage) && stopOnFirstError)
                        {
                            throw new InvalidOperationException($"行 {rowIndex} 批量属性设置失败: {failure.ErrorMessage}");
                        }
                    }

                    // 2) 映射Excel列到实体属性
                    foreach (var (propName, colIdx) in propertyToIndex)
                    {
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

                        // 若批量属性覆盖Excel值，延迟赋值
                        if (!batchOverridesExcel)
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

                    // 3) 应用批量属性并覆盖Excel值
                    if (batchProperties != null && batchOverridesExcel)
                    {
                        ApplyBatchProperties(record, batchProperties, dynamicBatchProperties, rowIndex, propertyCache, setterCache, log, ref failure);
                        if (!string.IsNullOrEmpty(failure.ErrorMessage) && stopOnFirstError)
                        {
                            throw new InvalidOperationException($"行 {rowIndex} 批量属性设置失败: {failure.ErrorMessage}");
                        }
                    }

                    // 4) 处理其他未映射列（存储为JSON字符串）
                    if (otherColumnProperty != null && columnMapping.Count < fieldCount)
                    {
                        for (var i = 0; i < fieldCount; i++)
                        {
                            var header = headers[i];
                            var mapped = columnMapping.Any(kv => string.Equals(kv.Key, header, StringComparison.OrdinalIgnoreCase));
                            if (mapped) continue;
                            otherDict[header] = rowValues[i];
                        }
                        if (otherDict.Count > 0)
                        {
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
                    }

                    // 5) 验证数据，并决定是否继续
                    var processStatus = ExcelRecordProcessStatus.Continue;
                    if (onRecordValidating != null)
                    {
                        try
                        {
                            processStatus = onRecordValidating(rowIndex, record, failure.RowValues, otherDict, ref failure);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 验证回调 onRecordValidating 抛异常：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                            failure.Exception = ex;
                            processStatus = ExcelRecordProcessStatus.Fail;
                        }
                    }
                    
                    // 根据验证状态分支处理（与动态类型方法逻辑一致）
                    switch (processStatus)
                    {
                        case ExcelRecordProcessStatus.Skip:
                            rowIndex++;
                            continue;
                        case ExcelRecordProcessStatus.Fail:
                            if (string.IsNullOrWhiteSpace(failure.ErrorMessage)) 
                                failure.ErrorMessage = $"行 {rowIndex} 业务验证不通过，标记为失败";
                            break;
                        case ExcelRecordProcessStatus.Abort:
                            if (string.IsNullOrWhiteSpace(failure.ErrorMessage)) 
                                failure.ErrorMessage = $"行 {rowIndex} 业务验证不通过，终止所有导入处理";
                            log?.Invoke(failure.ErrorMessage);
                            // 填充原始行值
                            for (var i = 0; i < fieldCount; i++)
                            {
                                var header = headers[i];
                                if (!failure.RowValues.ContainsKey(header))
                                    failure.RowValues[header] = rowValues[i];
                            }
                            importResult.Failures.Add(failure);
                            if (stopOnFirstError)
                            {
                                throw new InvalidOperationException(failure.ErrorMessage, failure.Exception);
                            }
                            else
                            {
                                log?.Invoke("已触发Abort指令，终止后续行处理");
                                return;
                            }
                        case ExcelRecordProcessStatus.Continue:
                        default:
                            break;
                    }

                    // 6) 完成转换后，继续处理数据
                    try
                    {
                        onRecordCreated?.Invoke(rowIndex, record, otherDict);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"行 {rowIndex} 回调 onRecordCreated 抛异常：{ex.Message}";
                        log?.Invoke(msg);
                        failure.ErrorMessage ??= string.Empty;
                        failure.ErrorMessage += msg + "; ";
                    }

                    // 7) 结果分类
                    if (!string.IsNullOrWhiteSpace(failure.ErrorMessage) || failure.Exception != null)
                    {
                        // 填充原始列值
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
                    // 捕获整行处理异常
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

            // 内部方法：确保属性与setter已缓存
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

            log?.Invoke($"当前Sheet强类型数据处理完成，共遍历 {rowIndex} 行数据");
        }

        /// <summary>
        /// 处理单个Sheet的动态对象数据（动态类型专用，修复变量错误，与泛型逻辑对齐）
        /// </summary>
        /// <param name="reader">ExcelDataReader实例</param>
        /// <param name="columnMapping">列映射关系</param>
        /// <param name="otherColumnsMappingName">未映射列属性名</param>
        /// <param name="stopOnFirstError">遇错即停</param>
        /// <param name="log">日志委托</param>
        /// <param name="batchProperties">批量固定属性</param>
        /// <param name="dynamicBatchProperties">动态批量属性</param>
        /// <param name="batchOverridesExcel">批量属性是否覆盖Excel值</param>
        /// <param name="onRecordCreated">创建回调</param>
        /// <param name="onRecordValidating">验证回调</param>
        /// <param name="result">导入结果对象</param>
        private static void ProcessDynamicSheetData(
            IExcelDataReader reader,
            Dictionary<string, string>? columnMapping,
            string? otherColumnsMappingName,
            bool stopOnFirstError, // 修复：移除无效参数，统一参数顺序
            Action<string>? log,
            IDictionary<string, object?>? batchProperties,
            IDictionary<string, Func<int, object?>>? dynamicBatchProperties,
            bool batchOverridesExcel,
            Action<dynamic, int>? onRecordCreated,
            ExcelDynamicRecordValidatingCallback? onRecordValidating,
            ExcelImportResult<dynamic> result)
        {
            if (!reader.Read())
            {
                log?.Invoke("当前Sheet为空或无可读行，直接返回空结果");
                return;
            }

            var fieldCount = reader.FieldCount;
            var headers = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                var raw = reader.GetValue(i);
                headers[i] = raw.ToString()?.Trim() ?? $"UnnamedColumn_{i}";
            }

            var headerToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var h = headers[i];
                headerToIndex.TryAdd(h, i);
            }

            // 构建映射：PropertyName -> columnIndex
            var columnIndexMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (columnMapping != null)
            {
                foreach (var excelCol in columnMapping.Keys)
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
                            log?.Invoke($"列映射警告：未找到 Excel 列 \"{excelCol}\" 映射到动态对象属性 {propName}");
                    }
                }
            }
            else
            {
                // 没有 columnMapping：若 otherColumnsMappingName 非空，按列名作为属性名映射
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
                var rowValuesArray = new object?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    var v = reader.GetValue(i);
                    rowValuesArray[i] = v == DBNull.Value ? null : v;
                }

                // 初始化动态对象和失败信息
                dynamic expando = new ExpandoObject();
                var expandoDict = expando as IDictionary<string, object?>;
                var failure = new ExcelImportFailure { RowIndex = rowIndex };
                // 先填充原始行值到failure（提前初始化，避免后续遗漏）
                for (var i = 0; i < fieldCount; i++)
                {
                    var header = headers[i];
                    if (!failure.RowValues.ContainsKey(header))
                        failure.RowValues[header] = rowValuesArray[i];
                }

                try
                {
                    // 1) 先应用批量属性（不覆盖Excel值）
                    if (batchProperties != null && !batchOverridesExcel)
                    {
                        try
                        {
                            ApplyBatchPropertiesToExpando(expandoDict, batchProperties, dynamicBatchProperties, rowIndex);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 批量属性设置失败：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                        }
                    }

                    // 2) 映射 Excel 列到 expando
                    foreach (var (key, colIdx) in columnIndexMapping)
                    {
                        var val = colIdx >= 0 && colIdx < rowValuesArray.Length ? rowValuesArray[colIdx] : null;
                        expandoDict?[key] = val;
                    }

                    // 3) 应用批量属性并覆盖Excel值
                    if (batchProperties != null && batchOverridesExcel)
                    {
                        try
                        {
                            ApplyBatchPropertiesToExpando(expandoDict, batchProperties, dynamicBatchProperties, rowIndex);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 批量属性（覆盖模式）设置失败：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                        }
                    }

                    // 4) 构建 otherColumns JSON 并写入 expando
                    if (!string.IsNullOrEmpty(otherColumnsMappingName))
                    {
                        var jsonObj = new ExpandoObject() as IDictionary<string, object?>;
                        for (var i = 0; i < fieldCount; i++)
                        {
                            if (columnIndexMapping.Values.Contains(i)) continue;
                            var header = headers[i];
                            var v = rowValuesArray[i];
                            jsonObj[header] = v?.ToString();
                        }
                        expandoDict[otherColumnsMappingName] = jsonObj;
                    }

                    // 5) 普通通知回调
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

                    // 6) 可控验证回调处理（与泛型方法逻辑一致）
                    var processStatus = ExcelRecordProcessStatus.Continue;
                    if (onRecordValidating != null)
                    {
                        try
                        {
                            processStatus = onRecordValidating(rowIndex, expando, failure.RowValues, ref failure);
                        }
                        catch (Exception ex)
                        {
                            var msg = $"行 {rowIndex} 验证回调 onRecordValidating 抛异常：{ex.Message}";
                            log?.Invoke(msg);
                            failure.ErrorMessage ??= string.Empty;
                            failure.ErrorMessage += msg + "; ";
                            failure.Exception = ex;
                            processStatus = ExcelRecordProcessStatus.Fail;
                        }
                    }

                    // 根据验证状态分支处理（与泛型方法完全对齐）
                    switch (processStatus)
                    {
                        case ExcelRecordProcessStatus.Skip:
                            log?.Invoke($"行 {rowIndex} 动态对象已被业务验证忽略");
                            rowIndex++;
                            continue; // 跳过本条，不记录成功/失败
                        case ExcelRecordProcessStatus.Fail:
                            if (string.IsNullOrWhiteSpace(failure.ErrorMessage))
                            {
                                failure.ErrorMessage = $"行 {rowIndex} 动态对象业务验证不通过，标记为失败";
                            }
                            // 记录失败明细
                            result.Failures.Add(failure);
                            log?.Invoke($"行 {rowIndex} 动态对象处理失败：{failure.ErrorMessage}");
                            // 遇错即停
                            if (stopOnFirstError)
                            {
                                log?.Invoke("已开启遇错即停模式，停止后续动态对象数据处理");
                                return;
                            }
                            break;
                        case ExcelRecordProcessStatus.Abort:
                            if (string.IsNullOrWhiteSpace(failure.ErrorMessage))
                            {
                                failure.ErrorMessage = $"行 {rowIndex} 动态对象业务验证不通过，终止所有导入处理";
                            }
                            log?.Invoke(failure.ErrorMessage);
                            // 记录失败并终止所有处理
                            result.Failures.Add(failure);
                            return;
                        case ExcelRecordProcessStatus.Continue:
                        default:
                            // 验证通过，加入成功列表
                            if (string.IsNullOrWhiteSpace(failure.ErrorMessage) && failure.Exception == null)
                            {
                                result.Items.Add(expando);
                            }
                            else
                            {
                                // 存在隐性错误，记录失败
                                result.Failures.Add(failure);
                                if (stopOnFirstError)
                                {
                                    log?.Invoke("已开启遇错即停模式，停止后续动态对象数据处理");
                                    return;
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 捕获未知异常，记录失败明细（与泛型方法逻辑一致）
                    failure.Exception = ex;
                    failure.ErrorMessage ??= ex.Message;
                    // 确保原始行值已填充
                    for (var i = 0; i < fieldCount; i++)
                    {
                        var header = headers[i];
                        if (!failure.RowValues.ContainsKey(header))
                            failure.RowValues[header] = rowValuesArray[i];
                    }

                    result.Failures.Add(failure);
                    log?.Invoke($"行 {rowIndex} 动态对象处理失败：{ex.Message}");
                    // 遇错即停
                    if (stopOnFirstError)
                    {
                        throw new InvalidOperationException($"在导入动态对象时检测到错误（行 {rowIndex}）：{ex.Message}", ex);
                    }
                }

                rowIndex++;
            }

            log?.Invoke($"当前Sheet动态对象数据处理完成，共遍历 {rowIndex} 行数据");
        }
        #endregion

        #region 内部辅助方法（批量属性应用+Setter创建，逻辑复用）
        /// <summary>
        /// 给泛型实体应用批量属性（固定+动态）
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="instance">实体实例</param>
        /// <param name="batchStatic">固定批量属性</param>
        /// <param name="batchDynamic">动态批量属性</param>
        /// <param name="rowIndex">当前行索引</param>
        /// <param name="propertyCache">属性缓存</param>
        /// <param name="setterCache">Setter缓存</param>
        /// <param name="log">日志委托</param>
        /// <param name="failure">失败信息</param>
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
            // 应用固定批量属性
            foreach (var (propName, rawVal) in batchStatic)
            {
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

            // 应用动态批量属性
            if (batchDynamic == null) return;

            foreach (var (propName, func) in batchDynamic)
            {
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

        /// <summary>
        /// 动态创建属性Setter委托（编译表达式，提升反射性能，内部复用）
        /// </summary>
        /// <param name="prop">属性信息</param>
        /// <returns>Setter委托</returns>
        private static Action<object, object?> CreateSetter(PropertyInfo prop)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);

            var convertMethod = typeof(ExcelTools).GetMethod(nameof(ConvertToTarget), BindingFlags.Public | BindingFlags.Static)!;
            var convertCall = Expression.Call(convertMethod, valueParam, Expression.Constant(prop.PropertyType, typeof(Type)));
            var converted = Expression.Convert(convertCall, prop.PropertyType);

            var call = Expression.Call(instanceCast, prop.SetMethod!, converted);
            var lambda = Expression.Lambda<Action<object, object?>>(call, instanceParam, valueParam);
            return lambda.Compile();
        }
        #endregion
    }
}