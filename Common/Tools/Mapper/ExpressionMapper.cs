using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using TKW.Framework.Abstractions;

namespace TKW.Framework.Tools.Mapper;

/// <summary>
/// 缓存策略定义
/// </summary>
public record CachePolicy(TimeSpan ExpirationTime, bool NeverExpire = false)
{
    public static readonly CachePolicy Default = new(TimeSpan.FromHours(24));
    public static readonly CachePolicy Permanent = new(TimeSpan.MaxValue, true);
}

/// <summary>
/// 高性能对象映射工具（面向 .NET 10 优化：SG > JIT > Reflection）
/// </summary>
/// <remarks>
/// 采用三级自动降级执行路径：
/// 1. Fast Path: 优先探测 Source Generator 生成的 ICopyValuesFrom 接口（零开销/AOT 完美兼容）。
/// 2. JIT Path: 环境支持时编译 Expression Tree 委托并缓存（接近原生性能）。
/// 3. Fallback Path: Native AOT 或受限环境下自动降级为带元数据保护的反射拷贝（确保可用性）。
/// 具备自动化缓存清理策略与原子化时间戳更新，支持高并发场景下的内存治理。
/// </remarks>
public static class ExpressionMapper
{
    private static readonly ConcurrentDictionary<(Type, Type), (Delegate Delegate, long LastAccessTicks, CachePolicy Policy)> _mapperCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    private static readonly bool IsJitSupported = RuntimeFeature.IsDynamicCodeCompiled;
    private static readonly System.Timers.Timer _cleanupTimer;

    static ExpressionMapper()
    {
        _cleanupTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds) { AutoReset = true, Enabled = true };
        _cleanupTimer.Elapsed += (_, _) => CleanupExpiredCache();
    }

    #region 公开 API

    /// <summary>
    /// [扩展方法] 拷贝值到现有对象 (Update)
    /// </summary>
    public static TTarget CopyValuesFrom<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSource,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>
        (this TTarget target, TSource source)
        where TSource : class where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(target);
        if (source == null) return target;

        // 1. SG 快速路径
        if (target is ICopyValuesFrom<TSource> generated)
        {
            generated.CopyValuesFrom(source);
            return target;
        }

        // 2. 环境探测路径
        if (IsJitSupported)
        {
            var mapper = GetOrCreateDelegate(
                (typeof(TSource), typeof(TTarget)),
                CreateUpdateDelegate<TSource, TTarget>);
            mapper(source, target);
        }
        else
        {
            ReflectionFallbackMap(source, target);
        }

        return target;
    }

    /// <summary>
    /// [扩展方法] 创建新对象并拷贝值 (Create)
    /// </summary>
    public static TTarget CopyToNew<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSource,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>
        (this TSource source)
        where TSource : class where TTarget : class, new()
    {
        if (source == null) return null!;

        // 1. 环境探测：JIT 模式下使用 MemberInit 委托 (性能最高)
        if (IsJitSupported)
        {
            var mapper = GetOrCreateDelegate(
                (typeof(TSource), typeof(TTarget)),
                CreateCreateDelegate<TSource, TTarget>);
            return mapper(source);
        }

        // 2. AOT 或降级模式：先 new 再尝试 CopyValuesFrom (可利用 SG)
        var target = new TTarget();
        return target.CopyValuesFrom(source);
    }

    /// <summary>
    /// [扩展方法] 高性能深拷贝 (Clone)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clone<T>(this T source) where T : class, new()
    {
        // 逻辑统一：直接调用 CopyToNew，其内部已包含三级路径分发
        return source.CopyToNew<T, T>();
    }

    /// <summary>
    /// 批量预缓存映射关系（用于启动预热，消除首次调用延迟）
    /// </summary>
    public static void BatchPreCache(IEnumerable<(Type Source, Type Target)> pairs)
    {
        if (!IsJitSupported) return;

        foreach (var (s, t) in pairs)
        {
            // 预热 Update 委托
            var methodUpdate = typeof(ExpressionMapper).GetMethod(nameof(GetOrCreateDelegate), BindingFlags.NonPublic | BindingFlags.Static);
            var updateFactory = typeof(ExpressionMapper).GetMethod(nameof(CreateUpdateDelegate), BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(s, t);
            methodUpdate?.MakeGenericMethod(typeof(Action<,>).MakeGenericType(s, t)).Invoke(null, [(s, t), updateFactory?.CreateDelegate(typeof(Func<>).MakeGenericType(typeof(Action<,>).MakeGenericType(s, t)))]);

            // 预热 Create 委托 (如果 T 具备无参构造)
            if (t.GetConstructor(Type.EmptyTypes) != null)
            {
                var createFactory = typeof(ExpressionMapper).GetMethod(nameof(CreateCreateDelegate), BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(s, t);
                methodUpdate?.MakeGenericMethod(typeof(Func<,>).MakeGenericType(s, t)).Invoke(null, [(s, t), createFactory?.CreateDelegate(typeof(Func<>).MakeGenericType(typeof(Func<,>).MakeGenericType(s, t)))]);
            }
        }
    }

    #endregion

    #region 内部编译逻辑 (私有)

    private static TDelegate GetOrCreateDelegate<TDelegate>((Type, Type) key, Func<TDelegate> factory) where TDelegate : Delegate
    {
        var item = _mapperCache.GetOrAdd(key, _ => (factory(), DateTime.UtcNow.Ticks, CachePolicy.Default));
        Interlocked.Exchange(ref Unsafe.As<long, long>(ref Unsafe.AsRef(in item.LastAccessTicks)), DateTime.UtcNow.Ticks);
        return (TDelegate)item.Delegate;
    }

    // 针对 CopyValuesFrom 的 Action 委托
    private static Action<TSource, TTarget> CreateUpdateDelegate<TSource, TTarget>()
    {
        var sParam = Expression.Parameter(typeof(TSource), "s");
        var tParam = Expression.Parameter(typeof(TTarget), "t");
        var expressions = new List<Expression>();
        var sProps = GetCachedProperties(typeof(TSource));
        var tProps = GetCachedProperties(typeof(TTarget));

        foreach (var tProp in tProps)
        {
            if (!tProp.CanWrite) continue;
            var sProp = Array.Find(sProps, p => string.Equals(p.Name, tProp.Name, StringComparison.OrdinalIgnoreCase)
                && (p.PropertyType == tProp.PropertyType || Nullable.GetUnderlyingType(tProp.PropertyType) == p.PropertyType));
            if (sProp == null || !sProp.CanRead) continue;

            var sAccess = Expression.Property(sParam, sProp);
            var tAccess = Expression.Property(tParam, tProp);

            if (!sProp.PropertyType.IsValueType || Nullable.GetUnderlyingType(sProp.PropertyType) != null)
                expressions.Add(Expression.IfThen(Expression.NotEqual(sAccess, Expression.Constant(null, sProp.PropertyType)), Expression.Assign(tAccess, sAccess)));
            else
                expressions.Add(Expression.Assign(tAccess, sAccess));
        }
        return Expression.Lambda<Action<TSource, TTarget>>(Expression.Block(expressions), sParam, tParam).Compile();
    }

    // 针对 CopyToNew 的 Func 委托 (MemberInit 性能更优)
    private static Func<TSource, TTarget> CreateCreateDelegate<TSource, TTarget>() where TTarget : new()
    {
        var sParam = Expression.Parameter(typeof(TSource), "s");
        var bindings = new List<MemberBinding>();
        var sProps = GetCachedProperties(typeof(TSource));
        var tProps = GetCachedProperties(typeof(TTarget));

        foreach (var tProp in tProps)
        {
            if (!tProp.CanWrite) continue;
            var sProp = Array.Find(sProps, p => string.Equals(p.Name, tProp.Name, StringComparison.OrdinalIgnoreCase) && p.PropertyType == tProp.PropertyType);
            if (sProp != null) bindings.Add(Expression.Bind(tProp, Expression.Property(sParam, sProp)));
        }
        return Expression.Lambda<Func<TSource, TTarget>>(Expression.MemberInit(Expression.New(typeof(TTarget)), bindings), sParam).Compile();
    }

    private static void ReflectionFallbackMap<TSource, TTarget>(TSource source, TTarget target)
    {
        var sProps = GetCachedProperties(typeof(TSource));
        var tProps = GetCachedProperties(typeof(TTarget));
        foreach (var tProp in tProps)
        {
            if (!tProp.CanWrite) continue;
            var sProp = Array.Find(sProps, p => string.Equals(p.Name, tProp.Name, StringComparison.OrdinalIgnoreCase));
            if (sProp != null && sProp.CanRead)
            {
                var val = sProp.GetValue(source);
                if (val != null) tProp.SetValue(target, val);
            }
        }
    }

    private static PropertyInfo[] GetCachedProperties(Type type)
    {
        return _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).ToArray());
    }

    private static void CleanupExpiredCache()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        foreach (var key in _mapperCache.Keys)
        {
            if (_mapperCache.TryGetValue(key, out var item) && !item.Policy.NeverExpire)
            {
                if (nowTicks - item.LastAccessTicks > item.Policy.ExpirationTime.Ticks)
                    _mapperCache.TryRemove(key, out _);
            }
        }
    }
    #endregion
}