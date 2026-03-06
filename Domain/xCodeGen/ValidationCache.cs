using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 高性能元数据与表达式缓存：实现元数据 O(1) 访问并消除重复编译开销
/// </summary>
public static class ValidationCache<T>
{
    /// <summary> 预缓存的属性元数据映射表 </summary>
    public static readonly IReadOnlyDictionary<string, PropertyMetadata> Meta;

    /// <summary> 表达式编译结果缓存 </summary>
    private static readonly ConcurrentDictionary<string, Delegate> _compiledCache = new ConcurrentDictionary<string, Delegate>();

    static ValidationCache()
    {
        // 自动识别类名（对齐 DTO 命名习惯）
        var type = typeof(T);
        var className = type.Name;
        if (className.EndsWith("Dto")) className = className.Substring(0, className.Length - 3);

        // 通过抽象基类单例获取已解析的字典
        if (ProjectMetaContextBase.Instance is ProjectMetaContextBase baseContext)
        {
            Meta = baseContext.GetPropertyMap(className);
        }
        else
        {
            Meta = new Dictionary<string, PropertyMetadata>();
        }
    }

    /// <summary> 获取或编译属性提取委托 </summary>
    public static Func<T, TValue> GetOrCompile<TValue>(Expression<Func<T, TValue>> expression)
    {
        var key = expression.ToString();
        return (Func<T, TValue>)_compiledCache.GetOrAdd(key, _ => expression.Compile());
    }
}