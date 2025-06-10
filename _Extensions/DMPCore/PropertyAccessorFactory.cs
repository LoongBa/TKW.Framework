using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace TKWF.DMP.Core;

/// <summary>
/// 使用表达式树缓存属性访问器，实现高性能的动态属性访问
/// </summary>
public static class PropertyAccessorFactory
{
    #region Getter 缓存

    private static readonly ConcurrentDictionary<(Type, string), Delegate> _getterCache = new();

    #endregion

    #region Setter 缓存

    private static readonly ConcurrentDictionary<(Type, string), Delegate> _setterCache = new();

    #endregion

    #region Getter 创建方法

    /// <summary>
    /// 创建属性 Getter 委托
    /// </summary>
    public static Func<T, TProperty> Create<T, TProperty>(string propertyName)
    {
        var key = (typeof(T), propertyName);

        if (_getterCache.TryGetValue(key, out var getter))
        {
            return (Func<T, TProperty>)getter;
        }

        var property = typeof(T).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        ArgumentNullException.ThrowIfNull(property, propertyName);

        var instance = Expression.Parameter(typeof(T), "instance");
        var propertyAccess = Expression.Property(instance, property);
        var convert = Expression.Convert(propertyAccess, typeof(TProperty));
        var lambda = Expression.Lambda<Func<T, TProperty>>(convert, instance);

        var compiled = lambda.Compile();
        _getterCache[key] = compiled;

        return compiled;
    }

    #endregion

    #region Setter 创建方法

    /// <summary>
    /// 创建属性 Setter 委托
    /// </summary>
    public static Action<T, TProperty> CreateSetter<T, TProperty>(string propertyName)
    {
        var key = (typeof(T), propertyName);

        if (_setterCache.TryGetValue(key, out var setter))
        {
            return (Action<T, TProperty>)setter;
        }

        var property = typeof(T).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (property == null || !property.CanWrite)
        {
            throw new ArgumentException($"类型 {typeof(T).Name} 不包含可写属性 {propertyName}");
        }

        var instance = Expression.Parameter(typeof(T), "instance");
        var value = Expression.Parameter(typeof(TProperty), "value");
        var propertyAccess = Expression.Property(instance, property);
        var convertedValue = Expression.Convert(value, property.PropertyType);
        var assign = Expression.Assign(propertyAccess, convertedValue);

        var lambda = Expression.Lambda<Action<T, TProperty>>(assign, instance, value);
        var compiled = lambda.Compile();

        _setterCache[key] = compiled;
        return compiled;
    }

    #endregion

    #region 非泛型版本（用于反射场景）

    /// <summary>
    /// 创建非泛型属性 Getter 委托
    /// </summary>
    public static Func<object, object> CreateGetter(string propertyName, Type type)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var instanceCast = Expression.Convert(instanceParam, type);
        var propertyInfo = type.GetProperty(propertyName);

        if (propertyInfo == null || !propertyInfo.CanRead)
        {
            throw new ArgumentException($"类型 {type.Name} 不包含可读属性 {propertyName}");
        }

        var propertyAccess = Expression.Property(instanceCast, propertyInfo);
        var resultCast = Expression.Convert(propertyAccess, typeof(object));

        var lambda = Expression.Lambda<Func<object, object>>(resultCast, instanceParam);
        return lambda.Compile();
    }

    /// <summary>
    /// 创建非泛型属性 Setter 委托
    /// </summary>
    public static Action<object, object> CreateSetter(string propertyName, Type type)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var instanceCast = Expression.Convert(instanceParam, type);
        var valueCast = Expression.Convert(valueParam, type.GetProperty(propertyName).PropertyType);

        var propertyInfo = type.GetProperty(propertyName);

        if (propertyInfo == null || !propertyInfo.CanWrite)
        {
            throw new ArgumentException($"类型 {type.Name} 不包含可写属性 {propertyName}");
        }

        var propertyAccess = Expression.Property(instanceCast, propertyInfo);
        var assign = Expression.Assign(propertyAccess, valueCast);

        var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
        return lambda.Compile();
    }

    #endregion
}