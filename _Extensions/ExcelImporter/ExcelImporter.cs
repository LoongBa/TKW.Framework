using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using ExcelDataReader;
using FluentValidation.Results;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace TKWF.ExcelImporter;

public class ExcelImporter<T> where T : class, new()
{
    public readonly ExcelTemplateRegistry TemplateRegistry;
    private readonly Encoding _ExcelEncoding;
    private readonly IExpressionEvaluator _ExpressionEvaluator;
    private readonly ILogger<ExcelImporter<T>>? _Logger;
    private readonly IStringLocalizer<ExcelImporter<T>>? _Localizer;
    private readonly int _ProgressStep = 100;   // 进度更新步长，可配置

    // 属性元数据缓存（反射性能优化）
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();

    // 高效属性setter缓存（表达式树）
    private static readonly ConcurrentDictionary<(Type, string), Action<object, object?>> _propertySetterCache = new();

    #region 事件定义
    public event EventHandler<RowValidationErrorEventArgs<T>>? RowValidationErrorOccurred;
    public event EventHandler<RowProcessingErrorEventArgs>? RowProcessingErrorOccurred;
    public event EventHandler<RowProgressUpdatedEventArgs>? RowProgressUpdated;
    #endregion

    #region 构造与初始化
    public ExcelImporter(
        string configDirectory,
        IExpressionEvaluator? expressionEvaluator = null,
        ILogger<ExcelImporter<T>>? logger = null,
        IStringLocalizer<ExcelImporter<T>>? localizer = null,
        Encoding? excelEncoding = null)
    {
        ArgumentNullException.ThrowIfNull(configDirectory);
        ArgumentNullException.ThrowIfNull(expressionEvaluator);

        _ExpressionEvaluator = expressionEvaluator switch
        {
            null => new ExpressionEvaluator(),
            _ => expressionEvaluator
        };

        _Logger = logger;
        _Localizer = localizer;
        TemplateRegistry = new ExcelTemplateRegistry(configDirectory);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _ExcelEncoding = excelEncoding ?? GetDefaultEncoding();
    }

    private static Encoding GetDefaultEncoding()
    {
        try { return Encoding.GetEncoding(1252); }
        catch { return Encoding.UTF8; }
    }
    #endregion

    #region 主导入方法
    public async Task<ImportResult<T>> ImportWithTemplateAsync(string filename, string templateId, bool ignoreValidation = true)
    {
        var result = new ImportResult<T>();
        var template = GetTemplateWithCache(templateId);
        var validator = new DynamicValidator<T>(template, _ExpressionEvaluator);

        if (!ValidateFileExistence(filename, result)) return result;

        _Logger?.LogInformation("开始导入文件: {File}，模板: {TemplateId}", filename, templateId);

        await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        using var reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = _ExcelEncoding })
            : ExcelReaderFactory.CreateReader(stream);

        try
        {
            // 表头验证
            var dataRowCount = reader.RowCount;
            string[] columnNames;

            if (template.HasHeader)
            {
                dataRowCount--;
                if (!reader.Read())
                {
                    var msg = _Localizer?["HeaderReadFailed"];
                    if (msg != null) result.Errors.Add(new ImportError(msg));
                    _Logger?.LogError("表头行读取失败: {File}", filename);
                    return result;
                }
                columnNames = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetString(i)?.Trim() ?? $"Column{i + 1}")
                    .ToArray();

                foreach (var mapping in template.ColumnMappings.Where(m => m.IsRequired))
                {
                    if (!columnNames.Contains(mapping.ExcelColumnName, StringComparer.OrdinalIgnoreCase))
                    {
                        var msg = _Localizer?["HeaderMissingColumn", mapping.ExcelColumnName];
                        if (msg != null) result.Errors.Add(new ImportError(msg, mapping.ExcelColumnName));
                        _Logger?.LogWarning("表头缺少必要列: {Column}", mapping.ExcelColumnName);
                    }
                }
                if (result.Errors.Count > 0) return result;
            }
            else
            {
                // 无表头时，列名按索引生成
                columnNames = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => $"Column{i + 1}")
                    .ToArray();
            }

            // 逐行处理
            ProcessRows(reader, template, columnNames, validator, result, dataRowCount, ignoreValidation, templateId);
        }
        catch (Exception ex)
        {
            var msg = _Localizer?["ImportFailed", ex.Message];
            if (msg != null) result.Errors.Add(new ImportError(msg + "\n" + ex.StackTrace));
            _Logger?.LogError(ex, "导入失败: {File}", filename);
        }

        _Logger?.LogInformation("导入完成: {File}，成功{Success}行，失败{Fail}行", filename, result.Data.Count, result.Errors.Count);
        return result;
    }
    #endregion

    #region 文件与表头校验
    private bool ValidateFileExistence(string filename, ImportResult<T> result)
    {
        if (!File.Exists(filename))
        {
            var msg = _Localizer?["FileNotFound", filename];
            if (msg != null) result.Errors.Add(new ImportError(msg));
            _Logger?.LogError("文件不存在: {File}", filename);
            return false;
        }
        return true;
    }
    #endregion

    #region 逐行处理
    private void ProcessRows(
        IExcelDataReader reader,
        ExcelTemplateConfiguration template,
        string[] columnNames,
        DynamicValidator<T> validator,
        ImportResult<T> result,
        int dataRowCount,
        bool ignoreValidation,
        string templateId)
    {
        var rowIndex = template.HasHeader ? 1 : 0;
        int successRows = 0, errorRows = 0, processedRows = 0;

        while (reader.Read())
        {
            processedRows++;
            try
            {
                var rowData = MapToModel(reader, template.ColumnMappings, columnNames, rowIndex, templateId);

                if (!ignoreValidation)
                {
                    var validationResult = validator.Validate(rowData);
                    var validationArgs = new RowValidationErrorEventArgs<T>(rowIndex, rowData, validationResult.Errors);
                    RowValidationErrorOccurred?.Invoke(this, validationArgs);
                    AddValidationErrors(result, rowIndex, validationResult);

                    if (!validationResult.IsValid)
                    {
                        errorRows++;
                        _Logger?.LogWarning("第{Row}行校验失败: {Errors}", rowIndex, string.Join(";", validationResult.Errors.Select(e => e.ErrorMessage)));
                        if (!validationArgs.ContinueProcessing)
                            break;
                        else
                            continue;
                    }
                }
                result.Data.Add(rowData);
                successRows++;
            }
            catch (FormatException ex)
            {
                errorRows++;
                var msg = _Localizer?.GetLocalizedError("RowFormatError", rowIndex, ex.Message, templateId);
                if (msg != null) result.Errors.Add(new ImportError(msg, fieldName: "", rowNumber: rowIndex));
                OnRowProcessingError(rowIndex, ex.ToString(), ex);
                _Logger?.LogError(ex, "第{Row}行数据格式错误，模板: {TemplateId}", rowIndex, templateId);
            }
            catch (InvalidOperationException ex)
            {
                errorRows++;
                var msg = _Localizer?.GetLocalizedError("RowMappingError", rowIndex, ex.Message, templateId);
                if (msg != null) result.Errors.Add(new ImportError(msg, fieldName: "", rowNumber: rowIndex));
                OnRowProcessingError(rowIndex, ex.ToString(), ex);
                _Logger?.LogError(ex, "第{Row}行映射失败，模板: {TemplateId}", rowIndex, templateId);
            }
            catch (Exception ex)
            {
                errorRows++;
                var msg = _Localizer?.GetLocalizedError("RowUnknownError", rowIndex, ex.Message, templateId);
                if (msg != null)
                    result.Errors.Add(new ImportError(msg + "\n" + ex.StackTrace, fieldName: "", rowNumber: rowIndex));
                OnRowProcessingError(rowIndex, ex.ToString(), ex);
                _Logger?.LogError(ex, "第{Row}行处理未知错误，模板: {TemplateId}", rowIndex, templateId);
            }

            // 进度事件频率控制
            if (processedRows % _ProgressStep == 0 || processedRows == dataRowCount)
            {
                OnRowProgressUpdated(dataRowCount, successRows, errorRows, processedRows);
            }
            rowIndex++;
        }
    }
    #endregion

    #region 模板缓存优化
    private static readonly ConcurrentDictionary<string, ExcelTemplateConfiguration> _templateCache = new();
    private ExcelTemplateConfiguration GetTemplateWithCache(string templateId)
    {
        return _templateCache.GetOrAdd(templateId, id => TemplateRegistry.GetTemplate(id));
    }
    #endregion

    #region 数据映射与转换
    private static Dictionary<string, PropertyInfo> GetPropertyMap()
    {
        return _propertyCache.GetOrAdd(typeof(T), t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase));
    }

    // 表达式树生成高效setter
    private static Action<object, object?> GetPropertySetter(Type type, string propertyName)
    {
        return _propertySetterCache.GetOrAdd((type, propertyName), _ =>
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return (_, _) => { };
            var instance = System.Linq.Expressions.Expression.Parameter(typeof(object), "instance");
            var value = System.Linq.Expressions.Expression.Parameter(typeof(object), "value");
            var body = System.Linq.Expressions.Expression.Assign(
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Convert(instance, type), prop),
                System.Linq.Expressions.Expression.Convert(value, prop.PropertyType));
            var lambda = System.Linq.Expressions.Expression.Lambda<Action<object, object?>>(body, instance, value);
            return lambda.Compile();
        });
    }

    private T MapToModel(IExcelDataReader reader, List<ColumnMapping> columnMappings, string[] columnNames, int rowIndex = 0, string? templateId = null)
    {
        var model = new T();
        var propertyMap = GetPropertyMap();

        foreach (var mapping in columnMappings)
        {
            if (!propertyMap.TryGetValue(mapping.TargetFieldName, out var property))
                continue;

            try
            {
                var columnIndex = Array.IndexOf(columnNames, mapping.ExcelColumnName);
                object? value = null;
                if (columnIndex >= 0 && columnIndex < reader.FieldCount)
                {
                    value = reader.GetValue(columnIndex);
                }
                else if (!string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    value = mapping.DefaultValue;
                }

                var convertedValue = ConvertValue(value, property.PropertyType, mapping.FormatPattern);
                // 使用表达式树setter
                GetPropertySetter(typeof(T), property.Name)(model, convertedValue);
            }
            catch (FormatException ex)
            {
                var errorMessage = _Localizer.GetLocalizedError("FieldFormatError", mapping.TargetFieldName, ex.Message, rowIndex, templateId ?? string.Empty);
                throw new FormatException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                var errorMessage = _Localizer.GetLocalizedError("FieldMappingError", mapping.TargetFieldName, ex.Message, rowIndex, templateId ?? string.Empty);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }
        return model;
    }

    private void AddValidationErrors(ImportResult<T> result, int rowIndex, ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            var importError = new ImportError(error.ErrorMessage, error.PropertyName, rowIndex);
            result.Errors.Add(importError);
        }
    }

    #endregion

    #region 类型转换相关（可扩展）
    public interface ITypeConverter
    {
        object? Convert(object? value, Type targetType, string? formatPattern = null);
    }

    private static ITypeConverter? _customTypeConverter;

    public static void RegisterTypeConverter(ITypeConverter converter)
    {
        _customTypeConverter = converter;
    }

    private static object? ConvertValue(object? value, Type? targetType = null, string? formatPattern = null)
    {
        if (_customTypeConverter != null)
            return _customTypeConverter.Convert(value, targetType!, formatPattern);

        if (value is DBNull || value == null)
            return null;

        if (targetType == null)
            return value is string s ? s.Trim() : value;

        if (value is IConvertible convertibleValue)
        {
            try
            {
                return Convert.ChangeType(convertibleValue, targetType, CultureInfo.InvariantCulture);
            }
            catch (FormatException) { throw; }
            catch
            {
                // ignored
            }
        }

        return targetType switch
        {
            { } t when t == typeof(string) => value.ToString()?.Trim() ?? string.Empty,
            { } t when t == typeof(DateTime) || t == typeof(DateTime?) => ParseDateTime(value, formatPattern),
            { } t when t == typeof(bool) || t == typeof(bool?) => ParseBoolean(value),
            _ => ParseFromString(value, targetType, formatPattern)
        };
    }

    private static object? ParseDateTime(object value, string? formatPattern)
    {
        if (value is double excelDate)
            return DateTime.FromOADate(excelDate);
        if (value is int excelDateInt)
            return DateTime.FromOADate(excelDateInt);

        var str = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(str))
            return null;

        if (string.IsNullOrEmpty(formatPattern))
        {
            return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : null;
        }
        else
        {
            return DateTime.TryParseExact(
                str,
                formatPattern,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result)
                ? result
                : null;
        }
    }

    private static object? ParseBoolean(object value)
    {
        if (value is bool b) return b;
        var str = value.ToString()?.Trim().ToLowerInvariant();
        return str switch
        {
            "1" or "是" or "有" or "yes" => true,
            "0" or "否" or "无" or "no" => false,
            _ => bool.TryParse(str, out var result) ? result : null
        };
    }

    private static object? ParseFromString(object value, Type targetType, string? formatPattern)
    {
        var str = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(str))
            return null;

        return targetType switch
        {
            { } t when t == typeof(int) || t == typeof(int?) =>
                int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var intVal) ? intVal : null,

            { } t when t == typeof(decimal) || t == typeof(decimal?) =>
                decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal) ? decVal : null,

            { } t when t == typeof(double) || t == typeof(double?) =>
                double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var dblVal) ? dblVal : null,

            { } t when t == typeof(float) || t == typeof(float?) =>
                float.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var fltVal) ? fltVal : null,

            { } t when t == typeof(long) || t == typeof(long?) =>
                long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var longVal) ? longVal : null,

            { } t when t == typeof(DateTime) || t == typeof(DateTime?) =>
                string.IsNullOrEmpty(formatPattern)
                    ? (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtVal) ? dtVal : null)
                    : (DateTime.TryParseExact(str, formatPattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtVal2) ? dtVal2 : null),

            { } t when t == typeof(bool) || t == typeof(bool?) =>
                str switch
                {
                    "1" or "是" or "有" or "yes" => true,
                    "0" or "否" or "无" or "no" => false,
                    _ => bool.TryParse(str, out var boolVal) ? boolVal : null
                },

            _ => TryChangeType(str, targetType)
        };
    }
    private static object? TryChangeType(object value, Type targetType)
    {
        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region 事件触发
    private void OnRowValidationError(int rowIndex, T rowData, List<ValidationFailure> errors)
    {
        RowValidationErrorOccurred?.Invoke(this, new RowValidationErrorEventArgs<T>(rowIndex, rowData, errors));
    }

    // 增加异常对象参数，便于外部记录堆栈
    private void OnRowProcessingError(int rowNumber, string errorMessage, Exception? ex = null)
    {
        RowProcessingErrorOccurred?.Invoke(this, new RowProcessingErrorEventArgs(rowNumber, errorMessage));
        // 可扩展：如有需要可将 ex 传递给事件参数
    }

    private void OnRowProgressUpdated(int totalRows, int successRows, int errorRows, int processedRows)
    {
        RowProgressUpdated?.Invoke(this, new RowProgressUpdatedEventArgs(totalRows, processedRows, successRows, errorRows));
    }

    #endregion
}