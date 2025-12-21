using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace TKW.Framework.Common.Extensions;

[AttributeUsage(AttributeTargets.Property)]
public class FlattenIgnoreAttribute : Attribute { }

public static class ObjectDictExtensions
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>
    /// 对象递归展平为字典（支持嵌套、循环引用检测、特性过滤、键冲突抛异常）
    /// 仅适合中等复杂度场景，极复杂建议 AutoMapper
    /// </summary>
    public static Dictionary<string, string> ToFlatDictionary(this object obj,
        string prefix = "",
        HashSet<object> visited = null,
        Func<PropertyInfo, bool> additionalFilter = null)
    {
        if (obj == null) return new();

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) throw new InvalidOperationException("检测到循环引用");

        var dict = new Dictionary<string, string>();

        var type = obj.GetType();
        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        foreach (var prop in properties)
        {
            // 默认排除：索引器、只写、带 [FlattenIgnore]
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanRead) continue;
            if (Attribute.IsDefined(prop, typeof(FlattenIgnoreAttribute))) continue;
            if (additionalFilter != null && !additionalFilter(prop)) continue;

            var value = prop.GetValue(obj);
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            // 简单类型直接添加
            if (value == null || IsSimpleType(prop.PropertyType))
            {
                if (!dict.TryAdd(key, value?.ToString()))
                    throw new InvalidOperationException($"键冲突: {key}");
            }
            else
            {
                // 递归嵌套对象
                var subDict = value.ToFlatDictionary(key + ".", visited, additionalFilter);
                foreach (var sub in subDict)
                {
                    if (dict.ContainsKey(sub.Key))
                        throw new InvalidOperationException($"键冲突: {sub.Key}");
                    dict[sub.Key] = sub.Value;
                }
            }
        }

        return dict;
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
        type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
}