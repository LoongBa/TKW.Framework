using ExcelDataReader;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TKW.Framework.Common.Enumerations;

namespace TKWF.Tools.ExcelTools;

#region 核心枚举/委托/数据结构

/// <summary>
/// 通用导入数据适配器接口
/// 统一各类数据源（不局限于Excel）的数据验证与转换逻辑，适配任意目标实体类型
/// </summary>
/// <typeparam name="TEntity">数据转换后的目标实体类型</typeparam>
public interface IImportDataAdapter<TEntity>
{
    /// <summary>
    /// 获取当前数据源名称（如：摩术师、美团券、抖音券）
    /// </summary>
    string DataSourceName { get; }

    /// <summary>
    /// 版本号（如：V1.0、V2.0）
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 获取当前数据源备注（如：适配摩术师V2.0版本、美团官方对账模板）
    /// </summary>
    string Remark { get; }

    /// <summary>
    /// 源数据列名 → 目标实体字段名 映射关系（只读）
    /// 用于定义数据源字段与目标实体字段的对应关系
    /// </summary>
    Dictionary<string, string> ColumnMapping { get; }

    /// <summary>
    /// 可选：用于存储未映射原始数据的实体属性名（如：RawDataJson）
    /// 若为null/空，则不存储未映射数据
    /// </summary>
    string? UnmappedDataStoragePropertyName { get; }

    /// <summary>
    /// 敏感字段名集合（序列化未映射数据时自动过滤）
    /// 如：MerchantKey、Password、AppSecret 等
    /// </summary>
    IReadOnlyCollection<string>? SensitiveFieldNames { get; }

    /// <summary>
    /// 数据业务验证逻辑：校验数据是否符合导入要求，不符合则返回对应处理状态
    /// </summary>
    /// <param name="rowIndex">数据行索引（用于定位错误数据位置）</param>
    /// <param name="targetEntity">待验证的目标实体对象</param>
    /// <param name="rowValues">原始数据行键值对字典</param>
    /// <param name="failure">校验失败信息（引用传递，用于输出错误详情）</param>
    /// <returns>导入数据处理状态指令（继续/跳过/终止）</returns>
    RecordValidateResultEnum ValidateData(int rowIndex, TEntity targetEntity,
        Dictionary<string, object?> rowValues, out ImportFailure? failure);

    /// <summary>
    /// 数据清洗、转换与加载逻辑：将原始数据处理后赋值到目标实体
    /// </summary>
    /// <param name="rowIndex">数据行索引（用于定位错误数据位置）</param>
    /// <param name="targetEntity">待赋值的目标实体对象</param>
    /// <param name="rawDict">未参与映射的原始数据键值对字典</param>
    /// <param name="unmappedDict">上下文参数字典（键值对，自动替换）</param>
    void ConvertData(int rowIndex, TEntity targetEntity,
        Dictionary<string, object?> rawDict, Dictionary<string, object?> unmappedDict);

    /// <summary>
    /// 核心加载方法（返回完整转换结果，流式处理）
    /// </summary>
    /// <param name="filename">文件路径</param>
    /// <param name="autoMapping">上下文参数字典（键值对，自动替换）</param>
    /// <returns>实体转换结果的异步迭代器（保留完整上下文）</returns>
    IAsyncEnumerable<EntityConvertResult<TEntity>> LoadDataAsync(
        string filename, Dictionary<string, string>? autoMapping = null);

    /// <summary>
    /// 核心加载方法（返回完整转换结果，从流式转为列表）
    /// </summary>
    /// <param name="filename">文件路径</param>
    /// <param name="autoMapping">上下文参数字典（键值对，自动替换）</param>
    /// <returns>实体转换结果的异步迭代器（保留完整上下文）</returns>
    Task<ImportResult<TEntity>> LoadDataToResultAsync(
        string filename, Dictionary<string, string>? autoMapping = null);
}
#endregion

#region Excel导入工具核心类

/// <summary>
/// Excel 导入工具（最终精简封装版）
/// 核心特性：
/// 1. 底层核心迭代器：ReadExcelAsResultsAsync（流式返回每行结果，无代码重复）
/// 2. 封装层级清晰：核心迭代器 → 适配器重载 → 汇总版本 → 汇总适配器版本
/// 3. 修复CS1626错误：yield return不在try/catch块内
/// 4. 高性能：类型转换委托缓存、流式读取低内存占用
/// 5. 完整错误处理：行级错误收集，支持流式/汇总两种返回方式
/// </summary>
public static class ExcelTools
{
    #region 性能优化：类型转换委托缓存（全局并发安全）
    private static readonly ConcurrentDictionary<Type, Func<object, Action<string>?, object?>> _typeConvertDelegateCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> _enumParseMethodCache = new();

    // 预定义基础类型常量
    private static readonly Type _guidType = typeof(Guid);
    private static readonly Type _dateTimeType = typeof(DateTime);
    private static readonly Type _boolType = typeof(bool);
    private static readonly Type _stringType = typeof(string);
    private static readonly Type _intType = typeof(int);
    private static readonly Type _longType = typeof(long);
    private static readonly Type _decimalType = typeof(decimal);
    #endregion

    #region 底层核心API：行级结果迭代器（所有上层方法的基础）

    /// <summary>
    /// 底层核心：流式读取Excel并返回每行的处理结果（成功/失败）
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="columnMapping">列映射（Excel列名 -> 实体属性名）</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="unmappedJsonColumnName">其他未映射列的存储属性名（JSON格式）</param>
    /// <param name="onValidateData">数据验证回调（可控流程）</param>
    /// <param name="onConvertData">数据转换回调（通知型）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>每行的处理结果迭代器（EntityConvertResult）</returns>
    public static async IAsyncEnumerable<EntityConvertResult<T>> ReadExcelAsResultsAsync<T>(string filename,
        Dictionary<string, string> columnMapping,
        int sheetIndex = 0,
        string? unmappedJsonColumnName = null,
        RecordValidatingCallback<T>? onValidateData = null,
        RecordConvertingCallback<T>? onConvertData = null,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        await Task.CompletedTask;

        // 1. 参数校验
        EntityConvertResult<T>? paramErrorResult = null;
        try
        {
            ValidateInputParameters(filename, columnMapping, sheetIndex);
        }
        catch (Exception ex)
        {
            paramErrorResult = new EntityConvertResult<T>
            {
                Success = false,
                RowIndex = 0,
                Failure = new ImportFailure
                {
                    RowIndex = 0,
                    ErrorMessage = $"参数校验失败：{ex.Message}",
                    Exception = ex
                }
            };
        }
        // Yield return 移出try块
        if (paramErrorResult != null)
        {
            yield return paramErrorResult;
            yield break;
        }

        // 2. 初始化基础资源
        var mapping = NormalizeColumnMapping(columnMapping);
        var propertySetters = CreatePropertySettersCache<T>();
        Stream? stream = null;
        IExcelDataReader? reader = null;
        List<string>? headers = null;
        EntityConvertResult<T>? initErrorResult = null;

        // 资源初始化的try块（内部无任何yield）
        try
        {
            // 初始化Excel读取资源
            stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = CreateExcelReader(filename, stream);
            log?.Invoke($"开始读取Excel文件：{filename}，Sheet索引：{sheetIndex}");

            // 定位到指定Sheet
            NavigateToSheet(reader, sheetIndex, log);

            // 读取表头
            headers = ReadHeaders(reader);

            // 检查表头是否为空（记录错误，不直接yield）
            if (headers.Count == 0)
            {
                initErrorResult = new EntityConvertResult<T>
                {
                    Success = false,
                    RowIndex = 0,
                    Failure = new ImportFailure { RowIndex = 0, ErrorMessage = "Excel表头为空，无法读取数据" }
                };
            }
            else
            {
                // 创建表头索引映射
                CreateHeaderIndexMap(headers);
            }
        }
        catch (Exception ex)
        {
            // 记录初始化错误，不直接yield
            initErrorResult = new EntityConvertResult<T>
            {
                Success = false,
                RowIndex = 0,
                Failure = new ImportFailure
                {
                    RowIndex = 0,
                    ErrorMessage = $"Excel初始化失败：{ex.Message}",
                    Exception = ex
                }
            };
        }

        // 处理初始化错误
        if (initErrorResult != null)
        {
            // 释放资源：IExcelDataReader 只有同步Dispose（第三方库限制）
            reader?.Dispose();
            // Stream 使用异步DisposeAsync
            if (stream != null) await stream.DisposeAsync();

            // Yield return 完全在try块外部
            yield return initErrorResult;
            yield break;
        }

        // 3. 迭代读取每行数据（核心循环）
        var rowIndex = 1;
        var isTerminated = false;

        while (!isTerminated && reader!.Read())
        {
            EntityConvertResult<T> rowResult;

            // 行处理的try块（内部无yield）
            try
            {
                // 读取原始数据
                var (rawData, unmappedDict) = ReadRowRawData(reader, headers!, mapping, unmappedJsonColumnName);
                if (unmappedJsonColumnName != null)
                    mapping.TryAdd(unmappedJsonColumnName, unmappedJsonColumnName);
                //处理批量替换的内容
                if (autoMappingProperties != null)
                    foreach (var property in autoMappingProperties)
                        if (rawData.ContainsKey(property.Key))
                            rawData[property.Key] = property.Value;

                var validateResult = RecordValidateResultEnum.Skip;
                // 初始化实体
                var entity = new T();
                var success = PopulateEntity(entity, rawData, mapping, rowIndex, propertySetters, log, out var errorMessage);

                // 执行验证回调
                ImportFailure? validateFailure = null;
                if (success && onValidateData != null)
                    validateResult = onValidateData(rowIndex, entity, rawData, out validateFailure);

                // 执行转换回调
                if (validateResult == RecordValidateResultEnum.Keep && onConvertData != null)
                {
                    try
                    {
                        onConvertData(rowIndex, entity, rawData, unmappedDict);
                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"转换回调执行失败：{ex.Message}";
                    }
                }

                // 封装行级结果
                if (validateResult == RecordValidateResultEnum.Keep)
                {
                    rowResult = ProcessValidateResult(entity, rawData, rowIndex, validateResult, validateFailure);
                }
                else
                {
                    rowResult = new EntityConvertResult<T>
                    {
                        Success = false,
                        RowIndex = rowIndex,
                        Failure = new ImportFailure
                        {
                            RowIndex = rowIndex,
                            ErrorMessage = errorMessage,
                            RowValues = rawData
                        }
                    };
                }

                // 标记是否终止
                isTerminated = validateResult == RecordValidateResultEnum.Terminate;
            }
            catch (Exception ex)
            {
                // 捕获行处理异常，封装为失败结果
                rowResult = new EntityConvertResult<T>
                {
                    Success = false,
                    RowIndex = rowIndex,
                    Failure = new ImportFailure
                    {
                        RowIndex = rowIndex,
                        ErrorMessage = $"行处理异常：{ex.Message}",
                        Exception = ex,
                        RowValues = new Dictionary<string, object?>()
                    }
                };
            }

            // 关键修复：yield return 完全在try块外部（彻底解决CS1626）
            yield return rowResult;
            rowIndex++;
        }

        // 4. 资源清理
        // 释放资源：IExcelDataReader 只有同步Dispose（第三方库限制）
        reader?.Dispose();
        // Stream 使用异步DisposeAsync
        if (stream != null) await stream.DisposeAsync();
        log?.Invoke($"Excel读取完成，总行数：{rowIndex - 1}");
    }

    /// <summary>
    /// 适配器重载版：基于核心迭代器，适配IImportDataAdapter接口
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="adapter">导入数据适配器</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>每行的处理结果迭代器</returns>
    public static async IAsyncEnumerable<EntityConvertResult<T>> ReadExcelAsResultsAsync<T>(
        string filename,
        IImportDataAdapter<T> adapter,
        int sheetIndex = 0,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        // 适配器转换为委托，调用核心迭代器方法
        await foreach (var result in ReadExcelAsResultsAsync<T>(
                           filename,
                           adapter.ColumnMapping,
                           sheetIndex,
                           adapter.UnmappedDataStoragePropertyName,
                           onValidateData: (rowIndex, entity, rawData, out failure)
                               => adapter.ValidateData(rowIndex, entity, rawData, out failure),
                           onConvertData: adapter.ConvertData,
                           autoMappingProperties: null,
                           log: log))
        {
            yield return result;
        }
    }
    #endregion

    #region 上层封装API：仅返回成功实体的迭代器（兼容原有逻辑）
    /// <summary>
    /// 流式读取Excel并返回成功的实体（基于核心迭代器封装）
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="columnMapping">列映射（Excel列名 -> 实体属性名）</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="unmappedJsonColumnName">其他未映射列的存储属性名（JSON格式）</param>
    /// <param name="onValidateData">数据验证回调（可控流程）</param>
    /// <param name="onConvertData">数据转换回调（通知型）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>成功的实体迭代器</returns>
    public static async IAsyncEnumerable<T> ReadExcelAsEntitiesAsync<T>(
        string filename,
        Dictionary<string, string> columnMapping,
        int sheetIndex = 0,
        string? unmappedJsonColumnName = null,
        RecordValidatingCallback<T>? onValidateData = null,
        RecordConvertingCallback<T>? onConvertData = null,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        // 基于核心迭代器，仅过滤返回成功的实体
        await foreach (var result in ReadExcelAsResultsAsync(
                           filename, columnMapping, sheetIndex, unmappedJsonColumnName,
                           onValidateData, onConvertData,
                           autoMappingProperties, log: log))
        {
            if (result.Success && result.Entity != null)
            {
                yield return result.Entity;
            }
        }
    }

    /// <summary>
    /// 适配器重载版：仅返回成功的实体（基于核心迭代器封装）
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="adapter">导入数据适配器</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>成功的实体迭代器</returns>
    public static async IAsyncEnumerable<T> ReadExcelAsEntitiesAsync<T>(
        string filename,
        IImportDataAdapter<T> adapter,
        int sheetIndex = 0,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        await foreach (var result in ReadExcelAsResultsAsync<T>(
                           filename: filename, columnMapping: adapter.ColumnMapping, sheetIndex: sheetIndex,
                           unmappedJsonColumnName: adapter.UnmappedDataStoragePropertyName,
                           autoMappingProperties: autoMappingProperties, log: log))
        {
            if (result.Success && result.Entity != null)
            {
                yield return result.Entity;
            }
        }
    }

    /// <summary>
    /// 读取Excel并返回成功的实体列表（基于核心迭代器封装）
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="columnMapping">列映射（Excel列名 -> 实体属性名）</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="unmappedJsonColumnName">其他未映射列的存储属性名（JSON格式）</param>
    /// <param name="onValidateData">数据验证回调（可控流程）</param>
    /// <param name="onConvertData">数据转换回调（通知型）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>成功的实体列表</returns>
    public static async Task<List<T>> ReadExcelAsEntityList<T>(
        string filename,
        Dictionary<string, string> columnMapping,
        int sheetIndex = 0,
        string? unmappedJsonColumnName = null,
        RecordValidatingCallback<T>? onValidateData = null,
        RecordConvertingCallback<T>? onConvertData = null,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        var result = new List<T>();
        await foreach (var entity in ReadExcelAsEntitiesAsync(
                           filename, columnMapping, sheetIndex, unmappedJsonColumnName, onValidateData, onConvertData, autoMappingProperties, log))
        {
            result.Add(entity);
        }
        return result;
    }
    #endregion

    #region 上层封装API：汇总结果版本（成功+失败）
    /// <summary>
    /// 汇总版本：读取Excel并返回完整的导入结果（成功列表+失败明细）
    /// 基于核心迭代器封装，无重复代码
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="columnMapping">列映射（Excel列名 -> 实体属性名）</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="unmappedJsonColumnName">其他未映射列的存储属性名（JSON格式）</param>
    /// <param name="onValidateData">数据验证回调（可控流程）</param>
    /// <param name="onConvertData">数据转换回调（通知型）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>ImportResult（成功列表+失败明细）</returns>
    public static async Task<ImportResult<T>> ReadExcelAsImportResultAsync<T>(
        string filename,
        Dictionary<string, string> columnMapping,
        int sheetIndex = 0,
        string? unmappedJsonColumnName = null,
        RecordValidatingCallback<T>? onValidateData = null,
        RecordConvertingCallback<T>? onConvertData = null,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        var importResult = new ImportResult<T>();

        // 基于核心迭代器汇总所有结果
        await foreach (var rowResult in ReadExcelAsResultsAsync(
                           filename, columnMapping, sheetIndex, unmappedJsonColumnName, onValidateData, onConvertData,
                           autoMappingProperties, log: log))
        {
            if (rowResult.Success && rowResult.Entity != null)
            {
                importResult.Items.Add(rowResult.Entity);
            }
            else if (rowResult.Failure != null)
            {
                importResult.Failures.Add(rowResult.Failure);
            }
        }

        log?.Invoke($"Excel 读取汇总完成，成功：{importResult.SuccessCount}，失败：{importResult.FailedCount}");
        return importResult;
    }

    /// <summary>
    /// 汇总适配器版本：基于核心迭代器+适配器，返回完整导入结果
    /// </summary>
    /// <typeparam name="T">目标实体类型</typeparam>
    /// <param name="filename">Excel文件路径</param>
    /// <param name="adapter">导入数据适配器</param>
    /// <param name="sheetIndex">工作表索引（从0开始）</param>
    /// <param name="unmappedJsonColumnName">其他未映射列的存储属性名（JSON格式）</param>
    /// <param name="autoMappingProperties">自动映射的列名与属性名的映射（用于支持更多的列映射），在回调之前执行</param>
    /// <param name="log">日志委托</param>
    /// <returns>ImportResult（成功列表+失败明细）</returns>
    public static async Task<ImportResult<T>> ReadExcelAsImportResultAsync<T>(
        string filename,
        IImportDataAdapter<T> adapter,
        int sheetIndex = 0,
        string? unmappedJsonColumnName = null,
        Dictionary<string, string>? autoMappingProperties = null,
        Action<string>? log = null)
        where T : new()
    {
        var importResult = new ImportResult<T>();

        // 基于适配器版核心迭代器汇总结果
        await foreach (var rowResult in ReadExcelAsResultsAsync(filename: filename,
                           adapter: adapter, sheetIndex: sheetIndex, autoMappingProperties: autoMappingProperties, log: log))
        {
            if (rowResult.Success && rowResult.Entity != null)
            {
                importResult.Items.Add(rowResult.Entity);
            }
            else if (rowResult.Failure != null)
            {
                importResult.Failures.Add(rowResult.Failure);
            }
        }

        log?.Invoke($"Excel读取汇总完成（适配器版），成功：{importResult.SuccessCount}，失败：{importResult.FailedCount}");
        return importResult;
    }
    #endregion

    #region 私有辅助方法：核心逻辑支撑
    /// <summary>
    /// 处理验证结果，封装为行级结果（提取独立方法，简化核心循环）
    /// </summary>
    private static EntityConvertResult<T> ProcessValidateResult<T>(
        T entity,
        Dictionary<string, object?> rawData,
        int rowIndex,
        RecordValidateResultEnum validateResult,
        ImportFailure? validateFailure)
        where T : new()
    {
        return validateResult switch
        {
            RecordValidateResultEnum.Keep => new EntityConvertResult<T>
            {
                Success = true,
                RowIndex = rowIndex,
                Entity = entity
            },
            RecordValidateResultEnum.Skip => new EntityConvertResult<T>
            {
                Success = false,
                RowIndex = rowIndex,
                Failure = validateFailure ?? new ImportFailure
                {
                    RowIndex = rowIndex,
                    ErrorMessage = "验证不通过，已跳过",
                    RowValues = rawData
                }
            },
            RecordValidateResultEnum.Terminate => new EntityConvertResult<T>
            {
                Success = false,
                RowIndex = rowIndex,
                Failure = validateFailure ?? new ImportFailure
                {
                    RowIndex = rowIndex,
                    ErrorMessage = "验证触发终止",
                    RowValues = rawData
                }
            },
            _ => new EntityConvertResult<T>
            {
                Success = false,
                RowIndex = rowIndex,
                Failure = new ImportFailure
                {
                    RowIndex = rowIndex,
                    ErrorMessage = $"未知的验证结果：{validateResult}",
                    RowValues = rawData
                }
            }
        };
    }

    /// <summary>
    /// 输入参数校验
    /// </summary>
    private static void ValidateInputParameters(string filename, Dictionary<string, string> columnMapping, int sheetIndex)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentNullException(nameof(filename), "文件路径不能为空");
        if (!File.Exists(filename))
            throw new FileNotFoundException($"文件不存在：{filename}", filename);
        if (columnMapping == null || columnMapping.Count == 0)
            throw new ArgumentNullException(nameof(columnMapping), "列映射不能为空");
        if (sheetIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sheetIndex), "Sheet索引不能为负数");

        if (sheetIndex != 0 && !IsSheetIndexValid(filename, sheetIndex))
        {
            var sheetCount = GetExcelSheetCount(filename);
            throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                $"Sheet索引 {sheetIndex} 无效，文件共有 {sheetCount} 个Sheet（有效索引：0-{sheetCount - 1}）");
        }
    }

    /// <summary>
    /// 标准化列映射（忽略大小写，过滤空值）
    /// </summary>
    private static Dictionary<string, string> NormalizeColumnMapping(Dictionary<string, string> columnMapping)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in columnMapping)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;
            mapping[kv.Key.Trim()] = kv.Value.Trim();
        }
        return mapping;
    }

    /// <summary>
    /// 创建 ExcelReader
    /// </summary>
    private static IExcelDataReader CreateExcelReader(string filename, Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);
    }

    /// <summary>
    /// 定位到指定Sheet
    /// </summary>
    private static void NavigateToSheet(IExcelDataReader reader, int sheetIndex, Action<string>? log)
    {
        if (sheetIndex == 0) return;

        var currentIndex = 0;
        while (reader.NextResult() && currentIndex < sheetIndex)
        {
            currentIndex++;
        }

        if (currentIndex != sheetIndex)
            throw new InvalidOperationException($"无法定位到Sheet索引 {sheetIndex}");

        log?.Invoke($"已定位到Sheet：{reader.Name}（索引：{sheetIndex}）");
    }

    /// <summary>
    /// 读取表头
    /// </summary>
    private static List<string> ReadHeaders(IExcelDataReader reader)
    {
        var headers = new List<string>();
        if (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                headers.Add(reader.GetString(i).Trim());
            }
        }
        return headers;
    }

    /// <summary>
    /// 创建表头索引映射
    /// </summary>
    private static Dictionary<string, int> CreateHeaderIndexMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(headers[i]))
                map[headers[i]] = i;
        }
        return map;
    }

    /// <summary>
    /// 读取行原始数据
    /// </summary>
    private static (Dictionary<string, object?> rawDataDict, Dictionary<string, object?> unmappedDict) ReadRowRawData(
        IExcelDataReader reader,
        List<string> headers,
        Dictionary<string, string> mapping,
        string? unmappedJsonColumnName)
    {
        var rawData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var unmappedColumns = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var header = headers[i];
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

            if (mapping.ContainsKey(header))
                rawData[header] = value;
            else if (!string.IsNullOrWhiteSpace(unmappedJsonColumnName))
                unmappedColumns[header] = value;
        }

        if (!string.IsNullOrWhiteSpace(unmappedJsonColumnName) && unmappedColumns.Count > 0)
        {
            rawData[unmappedJsonColumnName] = JsonSerializer.Serialize(
                unmappedColumns, options: new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        return (rawData, unmappedColumns);
    }

    /// <summary>
    /// 检查Sheet索引是否有效
    /// </summary>
    private static bool IsSheetIndexValid(string filePath, int sheetIndex)
    {
        if (sheetIndex == 0) return true;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = CreateExcelReader(filePath, stream);

        var currentIndex = 0;
        do
        {
            if (currentIndex == sheetIndex)
                return true;
            currentIndex++;
        } while (reader.NextResult());

        return false;
    }

    /// <summary>
    /// 获取Excel Sheet数量
    /// </summary>
    private static int GetExcelSheetCount(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = CreateExcelReader(filePath, stream);

        var count = 0;
        do
        {
            count++;
        } while (reader.NextResult());

        return count;
    }

    /// <summary>
    /// 创建属性设置器缓存（编译表达式提升反射性能）
    /// </summary>
    private static Dictionary<string, Action<T, object?>> CreatePropertySettersCache<T>()
    {
        var setters = new Dictionary<string, Action<T, object?>>(StringComparer.OrdinalIgnoreCase);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanWrite) continue;

            var objParam = Expression.Parameter(typeof(T), "obj");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var convertExpr = Expression.Convert(valueParam, prop.PropertyType);
            var assignExpr = Expression.Assign(Expression.Property(objParam, prop), convertExpr);
            var setter = Expression.Lambda<Action<T, object?>>(assignExpr, objParam, valueParam).Compile();

            setters[prop.Name] = setter;
        }

        return setters;
    }

    /// <summary>
    /// 填充实体属性（精简版：返回是否成功 + out 错误信息）
    /// </summary>
    private static bool PopulateEntity<T>(
        T entity,
        Dictionary<string, object?> rawData,
        Dictionary<string, string> mapping,
        int rowIndex,
        Dictionary<string, Action<T, object?>> propertySetters,
        Action<string>? log,
        out string? errorMessage)
        where T : new()
    {
        errorMessage = null;
        var errors = new List<string>();

        foreach (var kv in mapping)
        {
            if (!rawData.TryGetValue(kv.Key, out var value) || value == null) continue;

            if (!propertySetters.TryGetValue(kv.Value, out var setter))
            {
                errors.Add($"属性 {kv.Value} 不存在或不可写");
                continue;
            }

            var targetProp = typeof(T).GetProperty(kv.Value, BindingFlags.Public | BindingFlags.Instance);
            if (targetProp == null)
            {
                errors.Add($"属性 {kv.Value} 不存在");
                continue;
            }

            var convertResult = ConvertToTargetWithResult(value, targetProp.PropertyType, log);
            if (!convertResult.Success)
            {
                errors.Add($"列 {kv.Key} 转换失败：{convertResult.ErrorMessage}");
                continue;
            }

            setter(entity, convertResult.Value);
        }

        if (errors.Count > 0)
        {
            errorMessage = string.Join("；", errors);
            log?.Invoke($"行 {rowIndex} 实体填充失败：{errorMessage}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 获取类型专属转换委托（带缓存，高频调用优化）
    /// </summary>
    private static Func<object, Action<string>?, object?> GetTypeConvertDelegate(Type targetType)
    {
        return _typeConvertDelegateCache.GetOrAdd(targetType, CreateTypeConvertDelegateInternal);
    }

    /// <summary>
    /// 内部方法：创建类型专属转换委托（仅首次调用）
    /// </summary>
    private static Func<object, Action<string>?, object?> CreateTypeConvertDelegateInternal(Type nonNullableType)
    {
        // 1. 枚举类型：多维度解析（数值 > 英文名 > DisplayAttribute.Name）
        if (nonNullableType.IsEnum)
        {
            return (value, log) =>
            {
                try
                {
                    var input = value.ToString()!.Trim();
                    if (string.IsNullOrEmpty(input))
                    {
                        log?.Invoke($"枚举转换失败：输入值为空字符串，目标类型={nonNullableType.Name}，返回默认值");
                        return GetDefaultOrNull(nonNullableType);
                    }

                    var parseMethod = _enumParseMethodCache.GetOrAdd(nonNullableType, type => typeof(EnumHelper)
                        .GetMethod(nameof(EnumHelper.ParseEnum), [_stringType])!
                        .MakeGenericMethod(type));

                    var enumValue = parseMethod.Invoke(null, [input])!;
                    return enumValue;
                }
                catch (TargetInvocationException ex)
                {
                    var innerEx = ex.InnerException ?? ex;
                    log?.Invoke($"枚举转换失败【{value}→{nonNullableType.Name}】：{innerEx.Message}，返回默认值");
                    return GetDefaultOrNull(nonNullableType);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"枚举转换失败【{value}→{nonNullableType.Name}】：{ex.Message}，返回默认值");
                    return GetDefaultOrNull(nonNullableType);
                }
            };
        }

        // 2. Guid类型：鲁棒性增强
        if (nonNullableType == _guidType)
        {
            return (value, log) =>
            {
                if (value is Guid g) return g;

                var strValue = value.ToString()!.Trim();
                if (string.IsNullOrEmpty(strValue))
                {
                    log?.Invoke("Guid转换失败：输入值为空字符串，返回默认值");
                    return GetDefaultOrNull(_guidType);
                }

                if (Guid.TryParse(strValue, out var guid))
                {
                    return guid;
                }

                log?.Invoke($"Guid转换失败【{value}→Guid】：无效的Guid格式，返回默认值");
                return GetDefaultOrNull(_guidType);
            };
        }

        // 3. DateTime类型：增强兼容性
        if (nonNullableType == _dateTimeType)
        {
            return (value, log) =>
            {
                if (value is DateTime dt) return dt;

                var strValue = value.ToString()!.Trim();
                if (string.IsNullOrEmpty(strValue))
                {
                    log?.Invoke("DateTime转换失败：输入值为空字符串，返回默认值");
                    return GetDefaultOrNull(_dateTimeType);
                }

                var dateFormats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss" };
                if (DateTime.TryParse(strValue, out var date) ||
                    DateTime.TryParseExact(strValue, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return date;
                }

                log?.Invoke($"DateTime转换失败【{value}→DateTime】：不支持的日期格式，返回默认值");
                return GetDefaultOrNull(_dateTimeType);
            };
        }

        // 4. Boolean类型：增强业务兼容性
        if (nonNullableType == _boolType)
        {
            return (value, log) =>
            {
                if (value is bool b) return b;

                if (value is int i) return i != 0;
                if (value is long l) return l != 0;
                if (value is decimal d) return d != 0;

                var strValue = value.ToString()!.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(strValue))
                {
                    log?.Invoke("Boolean转换失败：输入值为空字符串，返回默认值");
                    return GetDefaultOrNull(_boolType);
                }

                if (strValue is "是" or "真" or "1" or "true" or "t") return true;
                if (strValue is "否" or "假" or "0" or "false" or "f") return false;

                log?.Invoke($"Boolean转换失败【{value}→bool】：不支持的布尔值格式，返回默认值");
                return GetDefaultOrNull(_boolType);
            };
        }

        // 5. 通用类型：兜底处理
        return (value, log) =>
        {
            try
            {
                if (nonNullableType.IsInstanceOfType(value))
                {
                    return value;
                }

                if (value is string s)
                {
                    var strValue = s.Trim();
                    if (string.IsNullOrEmpty(strValue))
                    {
                        return GetDefaultOrNull(nonNullableType);
                    }
                }

                return Convert.ChangeType(value, nonNullableType);
            }
            catch
            {
                try
                {
                    var strValue = value.ToString()!.Trim();
                    if (!string.IsNullOrEmpty(strValue))
                    {
                        var ctor = nonNullableType.GetConstructor([_stringType]);
                        if (ctor != null)
                        {
                            return ctor.Invoke([strValue]);
                        }
                    }
                }
                catch (Exception ctorEx)
                {
                    log?.Invoke($"构造函数转换失败【{value}→{nonNullableType.Name}】：{ctorEx.Message}");
                }

                log?.Invoke($"通用类型转换失败【{value}→{nonNullableType.Name}】，返回默认值");
                return GetDefaultOrNull(nonNullableType);
            }
        };
    }

    /// <summary>
    /// 类型转换核心方法（带结果封装+缓存优化）
    /// </summary>
    private static ConvertResult ConvertToTargetWithResult(object? value, Type targetType, Action<string>? log = null)
    {
        try
        {
            if (value == null)
            {
                return new ConvertResult
                {
                    Success = true,
                    Value = GetDefaultOrNull(targetType)
                };
            }

            if (targetType.IsInstanceOfType(value))
            {
                return new ConvertResult
                {
                    Success = true,
                    Value = value
                };
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                return new ConvertResult
                {
                    Success = true,
                    Value = GetDefaultOrNull(targetType)
                };
            }

            var convertDelegate = GetTypeConvertDelegate(nonNullable);
            var convertedValue = convertDelegate.Invoke(value, log);

            return new ConvertResult
            {
                Success = true,
                Value = convertedValue
            };
        }
        catch (Exception ex)
        {
            var errorMsg = $"转换失败【{value}→{targetType.Name}】：{ex.Message}";
            log?.Invoke(errorMsg);
            return new ConvertResult
            {
                Success = false,
                Value = GetDefaultOrNull(targetType),
                ErrorMessage = errorMsg
            };
        }
    }

    /// <summary>
    /// 获取类型默认值/Null
    /// </summary>
    private static object? GetDefaultOrNull(Type type)
    {
        if (!type.IsValueType) return null;
        if (Nullable.GetUnderlyingType(type) != null) return null;
        return Activator.CreateInstance(type);
    }


    #endregion
}
#endregion