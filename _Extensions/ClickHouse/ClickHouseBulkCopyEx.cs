using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace TKWF.Extensions.ClickHouse;

/// <summary>
/// 增强版 ClickHouse 批量复制工具，支持特性映射、表达式树缓存、嵌套属性、流式处理和配置热加载
/// </summary>
public class ClickHouseBulkCopyEx : ClickHouseBulkCopy
{
    private const int _DefaultBatchSize = 10000;

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ClickHouseColumnAttribute(string name) : Attribute
    {
        public string ColumnName { get; } = name;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ClickHouseIgnoreAttribute : Attribute;

    // 泛型类型转换器注册表
    private readonly ConcurrentDictionary<Type, Delegate> _converters = new();

    // 属性到列名的映射
    private readonly Dictionary<string, string> _propertyToColumnMap = new();

    // 泛型自定义属性映射委托
    private readonly Dictionary<string, Delegate> _typedCustomMappers = new();
    // 兼容object委托
    private readonly Dictionary<string, Func<object, object?>> _customMappers = new();

    // 条件映射
    private readonly Dictionary<string, Func<object, bool>> _conditionMappers = new();

    // 字段级类型转换器
    private readonly Dictionary<string, Func<object?, object?>> _fieldConverters = new();

    // 泛型属性访问器缓存
    private static readonly ConcurrentDictionary<(Type, int), Dictionary<string, Delegate>> _typedExpressionCache = new();

    private static readonly RecyclableMemoryStreamManager _memoryManager = new(new()
    {
        MaximumLargePoolFreeBytes = 512 * 1024 * 1024,
        MaximumSmallPoolFreeBytes = 128 * 1024 * 1024,
        BlockSize = 256 * 1024,
    });

    private string[]? _cachedColumnNames;

    public ILogger? Logger { get; set; }

    /// <summary>
    /// 简单日志委托，优先于 Logger
    /// </summary>
    public Action<string>? LogAction { get; set; }

    public ClickHouseBulkCopyEx(ClickHouseConnection connection) : base(connection)
    {
        MemoryStreamManager = _memoryManager;
        Initialize();
    }

    public ClickHouseBulkCopyEx(ClickHouseConnection connection, ILogger? logger = null, Action<string>? logAction = null)
        : base(connection)
    {
        Logger = logger;
        LogAction = logAction;
        MemoryStreamManager = _memoryManager;
        Initialize();
    }

    public ClickHouseBulkCopyEx(string connectionString) : base(connectionString)
    {
        MemoryStreamManager = _memoryManager;
        Initialize();
    }

    private void Initialize()
    {
        RegisterConverter<DateTime>(dt =>
            dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime()
                : dt.ToUniversalTime());
        RegisterConverter<decimal>(d => Math.Round(d, 2));
        RegisterConverter<Enum>(e => Convert.ToInt64(e));
        BatchSize = _DefaultBatchSize;
        MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
    }

    /// <summary>
    /// 泛型注册类型转换器，类型安全且高效
    /// </summary>
    public void RegisterConverter<T>(Func<T, object?> converter)
    {
        _converters[typeof(T)] = new Func<object, object?>(obj => converter((T)obj));
    }

    /// <summary>
    /// 泛型注册自定义属性映射委托
    /// </summary>
    public ClickHouseBulkCopyEx MapPropertyWithDelegate<T>(string propertyName, Func<T, object?> mapper)
    {
        _typedCustomMappers[propertyName] = mapper;
        return this;
    }

    /// <summary>
    /// 兼容object委托注册
    /// </summary>
    public ClickHouseBulkCopyEx MapPropertyWithDelegate(string propertyName, Func<object, object?> mapper)
    {
        _customMappers[propertyName] = mapper;
        return this;
    }

    public ClickHouseBulkCopyEx MapProperty(string propertyName, string columnName)
    {
        _propertyToColumnMap[propertyName] = columnName;
        return this;
    }

    public ClickHouseBulkCopyEx MapWhen(string propertyName, Func<object, bool> condition)
    {
        _conditionMappers[propertyName] = condition;
        return this;
    }

    public ClickHouseBulkCopyEx ConvertField(string fieldName, Func<object?, object?> converter)
    {
        _fieldConverters[fieldName] = converter;
        return this;
    }

    /// <summary>
    /// 泛型表达式树
    /// </summary>
    private static Func<T, object?> BuildTypedNestedAccessor<T>(string propertyPath)
    {
        var type = typeof(T);
        var param = Expression.Parameter(type, "obj");
        Expression current = param;
        foreach (var part in propertyPath.Split('.'))
        {
            var member = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance) as MemberInfo
                         ?? type.GetField(part, BindingFlags.Public | BindingFlags.Instance);
            if (member is PropertyInfo prop)
            {
                current = Expression.Property(current, prop);
                type = prop.PropertyType;
            }
            else if (member is FieldInfo field)
            {
                current = Expression.Field(current, field);
                type = field.FieldType;
            }
            else
            {
                throw new InvalidOperationException($"找不到嵌套属性/字段: {propertyPath}");
            }
        }
        var convert = Expression.Convert(current, typeof(object));
        return Expression.Lambda<Func<T, object?>>(convert, param).Compile();
    }

    /// <summary>
    /// 自动映射属性（支持无特性成员自动映射，支持嵌套路径）
    /// </summary>
    public ClickHouseBulkCopyEx AutoMapProperties<T>()
    {
        var type = typeof(T);
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance);

        foreach (var member in members)
        {
            if (member.GetCustomAttribute<ClickHouseIgnoreAttribute>() != null)
                continue;

            var columnName = member.GetCustomAttribute<ClickHouseColumnAttribute>()?.ColumnName;
            if (string.IsNullOrEmpty(columnName) && _propertyToColumnMap.TryGetValue(member.Name, out columnName))
            {
                // 已配置映射
            }
            else if (string.IsNullOrEmpty(columnName))
            {
                columnName = member.Name;
            }
            _propertyToColumnMap[member.Name] = columnName;
        }
        return this;
    }

    /// <summary>
    /// 批量写入数据（泛型委托优化，支持嵌套映射和流式处理，嵌套属性支持多级序列化）
    /// </summary>
    public async Task WriteToServerAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (string.IsNullOrWhiteSpace(DestinationTableName)) throw new InvalidOperationException("目标表未设置");

        if (ColumnNames == null || !ColumnNames.Any())
        {
            await InitAsync().ConfigureAwait(false);
        }

        var columnNames = ColumnNames?.ToArray() ?? GetColumnNames();
        var batchSize = BatchSize;
        if (batchSize <= 0) batchSize = _DefaultBatchSize; // 默认批量大小

        // 计算映射配置Hash，确保表达式缓存唯一性
        var mapHash = string.Join(",", _propertyToColumnMap.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}")).GetHashCode();
        var memberAccessors = GetTypedMemberAccessors<T>(mapHash, columnNames);

        var rows = new List<object[]>(batchSize);
        foreach (var entity in entities)
        {
            var row = new object[columnNames.Length];
            for (var i = 0; i < columnNames.Length; i++)
            {
                var columnName = columnNames[i];
                object? value = DBNull.Value;

                // 优先级：泛型自定义委托 > object委托 > 嵌套/属性/字段映射 > 条件映射 > 字段级转换 > 多级属性序列化 > 全局转换
                if (_typedCustomMappers.TryGetValue(columnName, out var typedMapper))
                {
                    value = ((Func<T, object?>)typedMapper)(entity);
                }
                else if (_customMappers.TryGetValue(columnName, out var objMapper))
                {
                    value = objMapper(entity!);
                }
                else if (memberAccessors.TryGetValue(columnName, out var accessor))
                {
                    value = accessor(entity);
                }

                // 条件映射
                if (_conditionMappers.TryGetValue(columnName, out var condition) && !condition(entity!))
                {
                    value = DBNull.Value;
                }

                // 字段级转换器
                if (_fieldConverters.TryGetValue(columnName, out var fieldConverter))
                {
                    value = fieldConverter(value);
                }

                // 嵌套属性序列化（仅当为复杂类型且未被自定义委托/转换器处理）
                if (value != null && value != DBNull.Value)
                {
                    value = SerializeIfComplex<T>(value, columnName);
                }

                // 全局类型转换器
                if (value != null && value != DBNull.Value)
                {
                    try
                    {
                        value = ApplyConverter(value);
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"ApplyConverter failed for column '{columnName}', value type: {value?.GetType().FullName}", ex);
                        value = DBNull.Value;
                    }
                }

                row[i] = value ?? DBNull.Value;
            }
            rows.Add(row);

            LogInfo("Row: " + string.Join(", ", row.Select(v => v.ToString() ?? "NULL")));

            if (rows.Count >= batchSize)
            {
                LogInfo($"Writing batch of {rows.Count} rows to table {DestinationTableName}...");
                await base.WriteToServerAsync(rows, token).ConfigureAwait(false);
                LogInfo($"Batch written to table {DestinationTableName}.");
                rows.Clear();
            }
        }
        if (rows.Any())
        {
            LogInfo($"Writing final batch of {rows.Count} rows to table {DestinationTableName}...");
            await base.WriteToServerAsync(rows, token).ConfigureAwait(false);
            LogInfo($"Final batch written to table {DestinationTableName}.");
        }
    }

    /// <summary>
    /// 嵌套属性序列化：优先调用接口，其次委托，最后通用序列化
    /// </summary>
    private object? SerializeIfComplex<T>(object value, string columnName)
    {
        var prop = ClickHouseMappingHelper.GetPropertyMap<T>().GetValueOrDefault(columnName);
        if (prop != null && prop.GetCustomAttribute<JsonIgnoreForClickHouseAttribute>() != null)
            return value;
        if (ClickHouseMappingHelper.IsSimpleType(value.GetType()))
            return value;
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString();
        }
    }

    /// <summary>
    /// 获取属性访问器（泛型）
    /// </summary>
    private Dictionary<string, Func<T, object?>> GetTypedMemberAccessors<T>(int mapHash, string[] columnNames)
    {
        var type = typeof(T);
        var cacheKey = (type, mapHash);
        if (_typedExpressionCache.TryGetValue(cacheKey, out var cached))
            return cached.ToDictionary(kv => kv.Key, kv => (Func<T, object?>)kv.Value);

        var accessors = new Dictionary<string, Delegate>();
        foreach (var col in columnNames)
        {
            var propertyPath = _propertyToColumnMap.FirstOrDefault(x => x.Value == col).Key ?? col;
            accessors[col] = BuildTypedNestedAccessor<T>(propertyPath);
        }
        _typedExpressionCache[cacheKey] = accessors;
        return accessors.ToDictionary(kv => kv.Key, kv => (Func<T, object?>)kv.Value);
    }

    public ClickHouseBulkCopyEx WithConcurrency(int concurrency)
    {
        MaxDegreeOfParallelism = concurrency;
        return this;
    }

    public ClickHouseBulkCopyEx WithBatchSize(int size)
    {
        BatchSize = size;
        return this;
    }

    /// <summary>
    /// 支持配置热加载（JSON文件变更自动刷新）
    /// </summary>
    public ClickHouseBulkCopyEx LoadMappingFromJson(string jsonPath, bool watch = false)
    {
        try
        {
            if (File.Exists(jsonPath))
            {
                LoadMappingInternal(jsonPath);
                if (watch)
                {
                    var watcher = new FileSystemWatcher(Path.GetDirectoryName(jsonPath)!, Path.GetFileName(jsonPath))
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    watcher.Changed += (_, e) =>
                    {
                        try { LoadMappingInternal(jsonPath); }
                        catch (Exception ex)
                        {
                            LogWarning($"Failed to load mapping from {jsonPath}:{e}", ex);
                        }
                    };
                    watcher.EnableRaisingEvents = true;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to read JSON file {jsonPath}", ex);
            throw;
        }
        return this;
    }

    private void LoadMappingInternal(string jsonPath)
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<MappingConfig>(File.ReadAllText(jsonPath));
        if (config != null)
        {
            foreach (var mapping in config.PropertyMappings)
            {
                MapProperty(mapping.PropertyName, mapping.ColumnName);
            }
            // 注意：FieldConverters 的委托无法通过 JSON 反序列化，需手动注册
        }
    }

    public new async Task InitAsync()
    {
        await base.InitAsync();
        var field = typeof(ClickHouseBulkCopy).GetField("columnNamesAndTypes", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var value = field.GetValue(this);
            if (value != null)
            {
                var item1Field = value.GetType().GetField("Item1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (item1Field != null)
                {
                    if (item1Field.GetValue(value) is string[] names)
                    {
                        // 去除每个列名中的所有反引号
                        _cachedColumnNames = names.Select(n => n.Replace("`", "")).ToArray();
                    }
                }
            }
        }
    }

    public string[] GetColumnNames() => _cachedColumnNames ?? [];

    public new void Dispose()
    {
        base.Dispose();
        _converters.Clear();
        _propertyToColumnMap.Clear();
        _typedCustomMappers.Clear();
        _customMappers.Clear();
        _conditionMappers.Clear();
        _fieldConverters.Clear();
    }
    /// <summary>
    /// 全局类型转换器应用
    /// </summary>
    private object? ApplyConverter(object? value)
    {
        if (value == null) return DBNull.Value;

        var type = value.GetType();
        if (_converters.TryGetValue(type, out var converter))
        {
            return ((Func<object, object?>)converter)(value);
        }

        // 如果是枚举类型，尝试转换为整型
        if (type.IsEnum)
        {
            return Convert.ToInt64(value);
        }

        return value;
    }
    // 日志输出统一封装
    private void LogInfo(string message)
    {
        if (LogAction != null)
            LogAction(message);
        else
            Logger?.LogInformation(message);
    }
    private void LogWarning(string message, Exception? ex = null)
    {
        if (LogAction != null)
            LogAction("[Warning] " + message + (ex != null ? "\n" + ex : ""));
        else
            Logger?.LogWarning(ex, message);
    }
    private void LogError(string message, Exception? ex = null)
    {
        if (LogAction != null)
            LogAction("[Error] " + message + (ex != null ? "\n" + ex : ""));
        else
            Logger?.LogError(message, ex);
    }
}