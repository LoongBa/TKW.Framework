using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Logging;

namespace TKWF.Extensions.ClickHouse;

public class JsonIgnoreForClickHouseAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TableNameForClickHouseAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// ClickHouse 通用仓储类，支持动态属性映射和批量写入
/// </summary>
public class ClickHouseRepository<T> where T : class, new()
{
    private readonly string? _connectionString;
    private readonly ClickHouseConnection? _externalConnection;
    private readonly ILogger? _logger;
    private readonly Action<string>? _logAction;

    /// <summary>
    /// 通过连接字符串构造
    /// </summary>
    public ClickHouseRepository(string connectionString, ILogger? logger = null, Action<string>? logAction = null)
    {
        _connectionString = connectionString;
        _logger = logger;
        _logAction = logAction;
    }

    /// <summary>
    /// 通过外部连接构造（支持连接共用）
    /// </summary>
    public ClickHouseRepository(ClickHouseConnection connection, ILogger? logger = null, Action<string>? logAction = null)
    {
        _externalConnection = connection;
        _connectionString = connection.ConnectionString;
        _logger = logger;
        _logAction = logAction;
    }

    private static readonly string TableName;
    private static readonly string[] ColumnNames;
    private static readonly Dictionary<string, PropertyInfo> PropertyMap;

    static ClickHouseRepository()
    {
        var type = typeof(T);
        var tableNameAttr = type.GetCustomAttribute<TableNameForClickHouseAttribute>();
        TableName = tableNameAttr?.Name ?? type.Name;
        ColumnNames = ClickHouseMappingHelper.GetColumnNames<T>();
        PropertyMap = ClickHouseMappingHelper.GetPropertyMap<T>();
        ColumnNames.ToDictionary(
            c => c,
            ClickHouseMappingHelper.GetAccessor<T>
        );
    }

    /// <summary>
    /// 获取连接（外部优先，内部自动释放）
    /// </summary>
    private ClickHouseConnection GetConnection(out bool needDispose)
    {
        if (_externalConnection != null)
        {
            needDispose = false;
            return _externalConnection;
        }
        needDispose = true;
        var conn = new ClickHouseConnection(_connectionString!);
        conn.Open();
        return conn;
    }

    private void LogError(string message, Exception? ex = null)
    {
        if (_logAction != null)
            _logAction("[Error] " + message + (ex != null ? "\n" + ex : ""));
        else
            _logger?.LogError(ex, message);
    }

    // 插入单条数据
    public void Insert(T entity) => BulkInsert([entity]);
    public async Task InsertAsync(T entity) => await BulkInsertAsync([entity]);

    // 批量插入（同步）
    public void BulkInsert(IEnumerable<T> entities)
    {
        BulkInsertAsync(entities).GetAwaiter().GetResult();
    }

    // 批量插入（异步）
    public async Task BulkInsertAsync(IEnumerable<T> entities)
    {
        var conn = GetConnection(out var needDispose);
        try
        {
            var bulk = new ClickHouseBulkCopyEx(conn, _logger, _logAction)
            {
                DestinationTableName = TableName,
                ColumnNames = ColumnNames
            };
            bulk.AutoMapProperties<T>();
            await bulk.WriteToServerAsync(entities);
        }
        catch (Exception ex)
        {
            LogError("BulkInsertAsync error", ex);
            throw;
        }
        finally
        {
            if (needDispose)
                conn.Dispose();
        }
    }

    // 查询（异步）
    public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate, int pageIndex = 0, int pageSize = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        var offset = pageIndex * pageSize;
        return await QueryAsync(whereClause, offset, pageSize);
    }

    private async Task<IEnumerable<T>> QueryAsync(string whereClause, int offset, int pageSize)
    {
        var conn = GetConnection(out var needDispose);
        try
        {
            var columns = string.Join(", ", ColumnNames);
            var sql = $"SELECT {columns} FROM {TableName} {(string.IsNullOrWhiteSpace(whereClause) ? "" : "WHERE " + whereClause)} LIMIT {pageSize} OFFSET {offset}";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync();
            var result = new List<T>();
            while (await reader.ReadAsync())
            {
                var obj = new T();
                for (var i = 0; i < ColumnNames.Length; i++)
                {
                    var prop = PropertyMap[ColumnNames[i]];
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    if (!ClickHouseMappingHelper.IsSimpleType(prop.PropertyType)
                        && val is string s
                        && prop.GetCustomAttribute<JsonIgnoreForClickHouseAttribute>() == null)
                    {
                        val = System.Text.Json.JsonSerializer.Deserialize(s, prop.PropertyType);
                    }
                    prop.SetValue(obj, val);
                }
                result.Add(obj);
            }
            return result;
        }
        catch (Exception ex)
        {
            LogError("QueryAsync error", ex);
            throw;
        }
        finally
        {
            if (needDispose)
                conn.Dispose();
        }
    }

    // 查询（同步）
    public IEnumerable<T> Query(Expression<Func<T, bool>> predicate, int pageIndex = 0, int pageSize = 100)
        => QueryAsync(predicate, pageIndex, pageSize).GetAwaiter().GetResult();

    // 单条查询（异步）
    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return (await QueryAsync(predicate, 0, 1)).FirstOrDefault();
    }

    // 单条查询（同步）
    public T? FirstOrDefault(Expression<Func<T, bool>> predicate)
        => FirstOrDefaultAsync(predicate).GetAwaiter().GetResult();

    public async Task<IEnumerable<T>> QueryTopAsync(Expression<Func<T, bool>> predicate, int count)
    {
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        return await QueryAsync(whereClause, 0, count);
    }

    public IEnumerable<T> QueryTop(Expression<Func<T, bool>> predicate, int count)
        => QueryTopAsync(predicate, count).GetAwaiter().GetResult();

    public async Task<IEnumerable<T>> QueryPagedAsync(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        var offset = (pageNumber - 1) * pageSize;
        return await QueryAsync(whereClause, offset, pageSize);
    }

    public IEnumerable<T> QueryPaged(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize)
        => QueryPagedAsync(predicate, pageNumber, pageSize).GetAwaiter().GetResult();

    // 删除（异步）
    public async Task DeleteAsync(Expression<Func<T, bool>> predicate)
    {
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        await DeleteAsync(whereClause);
    }

    private async Task DeleteAsync(string whereClause)
    {
        var conn = GetConnection(out var needDispose);
        try
        {
            var sql = $"ALTER TABLE {TableName} DELETE WHERE {whereClause}";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            LogError("DeleteAsync error", ex);
            throw;
        }
        finally
        {
            if (needDispose)
                conn.Dispose();
        }
    }

    // 删除（同步）
    public void Delete(Expression<Func<T, bool>> predicate)
        => DeleteAsync(predicate).GetAwaiter().GetResult();

    public bool DeleteOne(Expression<Func<T, bool>> predicate)
    {
        var record = FirstOrDefault(predicate);
        if (record == null) return false;
        Delete(predicate);
        return true;
    }

    public int DeleteMany(Expression<Func<T, bool>> predicate)
    {
        var count = Count(predicate);
        if (count > 0)
            Delete(predicate);
        return count;
    }

    // 更新（异步）
    public async Task UpdateAsync<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> property, TProperty value)
    {
        var member = (property.Body as MemberExpression)?.Member.Name
            ?? throw new ArgumentException("只支持简单属性赋值", nameof(property));
        var setClause = $"{member} = {ExpressionToClickHouseSql.FormatValue(value)}";
        await UpdateAsync(setClause, predicate);
    }

    private async Task UpdateAsync(string setClause, Expression<Func<T, bool>> predicate)
    {
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        await UpdateAsync(setClause, whereClause);
    }

    private async Task UpdateAsync(string setClause, string whereClause)
    {
        var conn = GetConnection(out var needDispose);
        try
        {
            var sql = $"ALTER TABLE {TableName} UPDATE {setClause} WHERE {whereClause}";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            LogError("UpdateAsync error", ex);
            throw;
        }
        finally
        {
            if (needDispose)
                conn.Dispose();
        }
    }

    // 更新（同步）
    public void Update<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> property, TProperty value)
        => UpdateAsync(predicate, property, value).GetAwaiter().GetResult();

    // 统计（异步）
    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var whereClause = ExpressionToClickHouseSql.ToSql(predicate);
        return await CountAsync(whereClause);
    }

    private async Task<int> CountAsync(string whereClause = "")
    {
        var conn = GetConnection(out var needDispose);
        try
        {
            var sql = $"SELECT COUNT(*) FROM {TableName} {(string.IsNullOrWhiteSpace(whereClause) ? "" : "WHERE " + whereClause)}";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            LogError("CountAsync error", ex);
            throw;
        }
        finally
        {
            if (needDispose)
                conn.Dispose();
        }
    }

    // 统计（同步）
    public int Count(Expression<Func<T, bool>> predicate)
        => CountAsync(predicate).GetAwaiter().GetResult();
}

/// <summary>
/// ClickHouse 映射与缓存工具，支持属性-列名映射、表达式树缓存等
/// </summary>
public static class ClickHouseMappingHelper
{
    private static readonly Dictionary<Type, string[]> _columnNameCache = new();
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();
    private static readonly ConcurrentDictionary<(Type, string), Delegate> _accessorCache = new();

    /// <summary>
    /// 判断类型是否为简单类型
    /// </summary>
    public static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(DateTime)
            || underlying == typeof(Guid)
            || underlying == typeof(decimal);
    }

    public static Func<T, object?> GetAccessor<T>(string propertyPath)
    {
        var key = (typeof(T), propertyPath);
        if (_accessorCache.TryGetValue(key, out var cached))
            return (Func<T, object?>)cached;

        var param = Expression.Parameter(typeof(T), "obj");
        Expression body = param;
        var type = typeof(T);
        foreach (var part in propertyPath.Split('.'))
        {
            var prop = type.GetProperty(part);
            if (prop == null) throw new InvalidOperationException($"属性 {part} 不存在");
            body = Expression.Property(body, prop);
            type = prop.PropertyType;
        }
        var convert = Expression.Convert(body, typeof(object));
        var lambda = Expression.Lambda<Func<T, object?>>(convert, param).Compile();
        _accessorCache[key] = lambda;
        return lambda;
    }

    /// <summary>
    /// 获取类型的所有映射列名（支持 ClickHouseColumnAttribute）
    /// </summary>
    public static string[] GetColumnNames<T>()
    {
        var type = typeof(T);
        if (_columnNameCache.TryGetValue(type, out var cached))
            return cached;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ClickHouseBulkCopyEx.ClickHouseIgnoreAttribute>() == null)
            .ToArray();

        var columns = props
            .Select(p => p.GetCustomAttribute<ClickHouseBulkCopyEx.ClickHouseColumnAttribute>()?.ColumnName ?? p.Name)
            .ToArray();

        _columnNameCache[type] = columns;
        return columns;
    }

    /// <summary>
    /// 获取类型的属性字典（属性名->PropertyInfo，忽略 ClickHouseIgnoreAttribute）
    /// </summary>
    public static Dictionary<string, PropertyInfo> GetPropertyMap<T>()
    {
        var type = typeof(T);
        if (_propertyCache.TryGetValue(type, out var cached))
            return cached;

        var dict = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ClickHouseBulkCopyEx.ClickHouseIgnoreAttribute>() == null)
            .ToDictionary(
                p => p.GetCustomAttribute<ClickHouseBulkCopyEx.ClickHouseColumnAttribute>()?.ColumnName ?? p.Name,
                p => p
            );
        _propertyCache[type] = dict;
        return dict;
    }
}

/// <summary>
/// 简单表达式转 ClickHouse SQL 工具（仅支持常用表达式，可扩展）
/// </summary>
public static class ExpressionToClickHouseSql
{
    public static string ToSql<T>(Expression<Func<T, bool>> expr)
    {
        return Visit(expr.Body);
    }

    private static string Visit(Expression expr)
    {
        return expr switch
        {
            BinaryExpression be => $"({Visit(be.Left)} {ToSqlOperator(be.NodeType)} {Visit(be.Right)})",
            MemberExpression me when me.Expression is ParameterExpression => me.Member.Name,
            ConstantExpression ce => FormatValue(ce.Value),
            MethodCallExpression mce => VisitMethodCall(mce),
            _ => throw new NotSupportedException($"不支持的表达式: {expr}")
        };
    }

    private static string ToSqlOperator(ExpressionType type) => type switch
    {
        ExpressionType.Equal => "=",
        ExpressionType.NotEqual => "<>",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "AND",
        ExpressionType.OrElse => "OR",
        _ => throw new NotSupportedException($"不支持的操作符: {type}")
    };

    public static string FormatValue(object? value)
    {
        if (value == null) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
        if (value is bool b) return b ? "1" : "0";
        return value.ToString()!;
    }

    private static string VisitMethodCall(MethodCallExpression mce)
    {
        // 支持 .Contains, .StartsWith, .EndsWith
        if (mce.Method.Name == "Contains" && mce.Object is MemberExpression me)
        {
            var member = Visit(me);
            var value = Visit(mce.Arguments[0]);
            return $"{member} LIKE '%' || {value} || '%'";
        }
        throw new NotSupportedException($"不支持的方法: {mce.Method.Name}");
    }
}