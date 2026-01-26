using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 导入适配器通用基类（封装未映射数据存储+敏感字段过滤，与具体数据源无关）
/// </summary>
/// <typeparam name="TEntity">目标实体类型</typeparam>
public abstract class ImportAdapterBase<TEntity> : IImportDataAdapter<TEntity>
    where TEntity : class, new()
{
    /// <summary>
    /// 日志记录器（可选）
    /// </summary>
    protected ILogger? Logger { get; set; }

    public abstract string DataSourceName { get; }
    public abstract string Version { get; }
    public abstract string Remark { get; }
    public abstract Dictionary<string, string> ColumnMapping { get; }

    /// <inheritdoc/>
    public virtual string? UnmappedDataStoragePropertyName { get; set; } = "RawDataJson";

    /// <inheritdoc/>
    public virtual IReadOnlyCollection<string> SensitiveFieldNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 默认敏感字段（所有适配器通用）
        "MerchantKey", "Password", "AppSecret", "ApiKey", "Token"
    };

    /// <inheritdoc/>
    public abstract RecordValidateResultEnum ValidateData(int rowIndex, TEntity targetEntity,
        Dictionary<string, object?> rowValues, out ImportFailure? failure);

    /// <inheritdoc/>
    public abstract void ConvertData(int rowIndex, TEntity targetEntity,
        Dictionary<string, object?> rawDict, Dictionary<string, object?> unmappedDict);

    /// <inheritdoc/>
    public abstract IAsyncEnumerable<EntityConvertResult<TEntity>> LoadDataAsync(
        string filename, Dictionary<string, string>? autoMapping = null);

    /// <inheritdoc/>
    public virtual async Task<ImportResult<TEntity>> LoadDataToResultAsync(string filename, Dictionary<string, string>? autoMapping = null)
    {
        var result = new ImportResult<TEntity>();

        await foreach (var record in LoadDataAsync(filename: filename, autoMapping: autoMapping))
        {
            if (record.Success)
            {
                // 注意：这里假设 ImportResult 内部处理了具体的 Entity 类型
                // 如果 Entity 是 object 类型，可能需要根据实际情况调整
                if (record.Entity != null)
                    result.Items.Add(record.Entity);
            }
            else if (record.Failure != null)
            {
                result.Failures.Add(record.Failure);
            }
        }

        return result;
    }
    /// <summary>
    /// 通用方法：提取未映射数据并序列化为JSON，存入指定实体属性（与Excel无关）
    /// </summary>
    /// <param name="rowIndex">行索引（用于日志定位）</param>
    /// <param name="targetEntity">目标实体</param>
    /// <param name="rawRowData">原始行数据</param>
    protected void StoreUnmappedDataAsJson(int rowIndex, TEntity targetEntity, Dictionary<string, object?> rawRowData)
    {
        // 前置校验：未配置存储属性则直接返回
        if (string.IsNullOrWhiteSpace(UnmappedDataStoragePropertyName))
        {
            Logger?.LogDebug("未配置未映射数据存储属性名，跳过存储 | 行号：{RowIndex}", rowIndex);
            return;
        }

        try
        {
            // 步骤1：筛选未映射字段（排除ColumnMapping中的目标字段）
            var mappedTargetFieldNames = ColumnMapping.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unmappedData = rawRowData
                .Where(kv => !mappedTargetFieldNames.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            // 步骤2：过滤敏感字段（保护隐私/安全数据）
            if (SensitiveFieldNames.Any())
            {
                var filteredFields = unmappedData.Keys.Intersect(SensitiveFieldNames).ToList();
                foreach (var field in filteredFields)
                {
                    unmappedData[field] = "***敏感数据已过滤***"; // 替换为占位符，而非删除（保留字段存在性）
                    Logger?.LogDebug("敏感字段已过滤 | 行号：{RowIndex} | 字段名：{FieldName}", rowIndex, field);
                }
            }

            // 步骤3：序列化为JSON（处理特殊类型）
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new JsonStringEnumConverter(), new DateTimeConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(unmappedData, jsonOptions);

            // 步骤4：反射赋值到指定实体属性
            var property = typeof(TEntity).GetProperty(
                UnmappedDataStoragePropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                Logger?.LogWarning(
                    "未映射数据存储失败 | 行号：{RowIndex} | 实体{EntityType}不存在属性{PropertyName}",
                    rowIndex, typeof(TEntity).Name, UnmappedDataStoragePropertyName);
                return;
            }

            if (!property.CanWrite)
            {
                Logger?.LogWarning(
                    "未映射数据存储失败 | 行号：{RowIndex} | 实体{EntityType}的属性{PropertyName}不可写",
                    rowIndex, typeof(TEntity).Name, UnmappedDataStoragePropertyName);
                return;
            }

            if (property.PropertyType != typeof(string))
            {
                Logger?.LogWarning(
                    "未映射数据存储失败 | 行号：{RowIndex} | 实体{EntityType}的属性{PropertyName}类型不是字符串（当前：{PropertyType}）",
                    rowIndex, typeof(TEntity).Name, UnmappedDataStoragePropertyName, property.PropertyType.Name);
                return;
            }

            // 最终赋值
            property.SetValue(targetEntity, json);
            Logger?.LogDebug(
                "未映射数据存储成功 | 行号：{RowIndex} | 实体{EntityType} | 属性{PropertyName} | 字段数：{FieldCount}",
                rowIndex, typeof(TEntity).Name, UnmappedDataStoragePropertyName, unmappedData.Count);
        }
        catch (Exception ex)
        {
            Logger?.LogError(
                ex,
                "未映射数据JSON序列化/存储失败 | 行号：{RowIndex} | 实体{EntityType} | 属性{PropertyName}",
                rowIndex, typeof(TEntity).Name, UnmappedDataStoragePropertyName);
            // 不抛出异常，避免影响核心转换逻辑
        }
    }

    /// <summary>
    /// DateTime类型JSON转换器（统一格式）
    /// </summary>
    private class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}