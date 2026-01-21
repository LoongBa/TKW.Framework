using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Timers;
using System.Collections.Generic;
using System.Threading;
using Timer = System.Timers.Timer;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 缓存清理策略（支持全局默认+类型对专属）
/// </summary>
public class CachePolicy
{
    /// <summary>
    /// 缓存过期时间（默认24小时）
    /// </summary>
    public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// 是否永不过期（核心类型建议设置为true）
    /// </summary>
    public bool NeverExpire { get; set; }

    /// <summary>
    /// 全局默认策略
    /// </summary>
    public static readonly CachePolicy Default = new();

    /// <summary>
    /// 永不过期策略（核心类型专用）
    /// </summary>
    public static readonly CachePolicy Permanent = new() { NeverExpire = true, ExpirationTime = TimeSpan.MaxValue };

    /// <summary>
    /// 短期策略（临时类型专用，默认1小时）
    /// </summary>
    public static readonly CachePolicy ShortTerm = new() { ExpirationTime = TimeSpan.FromHours(1) };
}

/// <summary>
/// 高性能对象映射工具（C#12+ 通用扩展方法 + Expression 树）
/// 特性：预缓存、自定义缓存策略、自动清理过期缓存
/// </summary>
public static class ExpressionMapper
{
    #region 缓存容器（带策略+访问时间）
    // 缓存1：类型属性信息 
    // Key=Type, Value=(PropertyInfo[], 最后访问时间, 缓存策略)
    private static readonly ConcurrentDictionary<Type, (PropertyInfo[] Props, DateTime LastAccessTime, CachePolicy Policy)> _propertyCache = new();

    // 缓存2：编译后的映射委托 
    // Key=(源类型,目标类型), Value=(委托, 最后访问时间, 缓存策略)
    private static readonly ConcurrentDictionary<(Type SourceType, Type TargetType), (Delegate Delegate, DateTime LastAccessTime, CachePolicy Policy)> _mapperDelegateCache = new();
    #endregion

    #region 清理配置
    /// <summary>
    /// 缓存清理全局配置
    /// </summary>
    public static class CleanupConfig
    {
        /// <summary>
        /// 自动清理间隔（默认1小时）
        /// </summary>
        public static TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// 是否启用自动清理（默认开启）
        /// </summary>
        public static bool EnableAutoCleanup { get; set; } = true;
    }
    #endregion

    #region 定时清理组件
    private static readonly Timer _cacheCleanupTimer;
    private static readonly ReaderWriterLockSlim _cleanupLock = new(LockRecursionPolicy.NoRecursion);

    static ExpressionMapper()
    {
        // 初始化定时清理Timer
        if (CleanupConfig.EnableAutoCleanup)
        {
            _cacheCleanupTimer = new Timer(CleanupConfig.CleanupInterval.TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = true
            };
            _cacheCleanupTimer.Elapsed += OnCacheCleanupTimerElapsed;

            // 应用退出时释放资源
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                _cacheCleanupTimer.Stop();
                _cacheCleanupTimer.Dispose();
                _cleanupLock.Dispose();
            };
        }
    }
    #endregion

    #region 核心扩展方法（C#12+ 通用扩展）
    /// <summary>
    /// 从源对象拷贝值到目标对象（仅拷贝同名同类型、可写、非null的属性，忽略大小写）
    /// </summary>
    /// <typeparam name="TSource">源类型</typeparam>
    /// <typeparam name="TTarget">目标类型</typeparam>
    extension<TSource, TTarget>(TTarget target) where TSource : class where TTarget : class
    {
        public TTarget CopyValuesFrom(TSource source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            var updateDelegate = GetOrCreateUpdateDelegate<TSource, TTarget>();
            updateDelegate(source, target);

            return target;
        }
    }

    /// <summary>
    /// 创建目标对象并从源对象拷贝值（目标类型需有无参构造函数）
    /// </summary>
    /// <typeparam name="TSource">源类型</typeparam>
    extension<TSource>(TSource source) where TSource : class
    {
        public TTarget CopyToNew<TTarget>() where TTarget : class, new()
        {
            if (source == null) return null;

            var createDelegate = GetOrCreateCreateDelegate<TSource, TTarget>();
            return createDelegate(source);
        }
    }
    #endregion

    #region 预缓存方法（支持自定义策略）
    /// <summary>
    /// 预缓存指定类型对的映射委托（系统启动时调用）
    /// </summary>
    /// <typeparam name="TSource">源类型</typeparam>
    /// <typeparam name="TTarget">目标类型</typeparam>
    /// <param name="policy">缓存策略（默认使用全局默认策略）</param>
    public static void PreCacheMapper<TSource, TTarget>(CachePolicy policy = null)
        where TSource : class
        where TTarget : class, new()
    {
        var actualPolicy = policy ?? CachePolicy.Default;

        // 预编译委托并缓存（带策略）
        GetOrCreateCreateDelegate<TSource, TTarget>(actualPolicy);

        // 预缓存属性信息（带策略）
        GetCachedProperties<TSource>(actualPolicy);
        GetCachedProperties<TTarget>(actualPolicy);

        Console.WriteLine($"预缓存完成 [{(actualPolicy.NeverExpire ? "永不过期" : $"过期时间:{actualPolicy.ExpirationTime}")}]：" +
                          $"{typeof(TSource).Name} → {typeof(TTarget).Name}");
    }

    /// <summary>
    /// 批量预缓存（传入类型对列表+统一策略）
    /// </summary>
    /// <param name="typePairs">类型对列表（SourceType, TargetType）</param>
    /// <param name="policy">缓存策略（默认使用全局默认策略）</param>
    public static void BatchPreCacheMapper(IEnumerable<(Type SourceType, Type TargetType)> typePairs, CachePolicy policy = null)
    {
        if (typePairs == null) throw new ArgumentNullException(nameof(typePairs), "类型对列表不能为空");

        var actualPolicy = policy ?? CachePolicy.Default;

        foreach (var (sourceType, targetType) in typePairs)
        {
            try
            {
                // 动态调用泛型预缓存方法
                var method = typeof(ExpressionMapper)
                    .GetMethod(nameof(PreCacheMapper), [typeof(CachePolicy)])
                    ?.MakeGenericMethod(sourceType, targetType);

                method?.Invoke(null, [actualPolicy]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量预缓存失败 {sourceType.Name} → {targetType.Name}：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 手动清理所有过期缓存
    /// </summary>
    public static void ManualCleanupExpiredCache()
    {
        OnCacheCleanupTimerElapsed(null, null);
    }

    /// <summary>
    /// 清空所有缓存（谨慎使用）
    /// </summary>
    public static void ClearAllCache()
    {
        try
        {
            _cleanupLock.EnterWriteLock();
            _propertyCache.Clear();
            _mapperDelegateCache.Clear();
            Console.WriteLine("所有缓存已清空");
        }
        finally
        {
            if (_cleanupLock.IsWriteLockHeld)
                _cleanupLock.ExitWriteLock();
        }
    }
    #endregion

    #region 内部核心：委托创建/缓存
    private static Func<TSource, TTarget> GetOrCreateCreateDelegate<TSource, TTarget>(CachePolicy policy = null)
        where TSource : class
        where TTarget : class, new()
    {
        var key = (typeof(TSource), typeof(TTarget));
        var actualPolicy = policy ?? CachePolicy.Default;

        // 缓存命中：更新访问时间
        if (_mapperDelegateCache.TryGetValue(key, out var cacheItem))
        {
            _mapperDelegateCache[key] = (cacheItem.Delegate, DateTime.Now, cacheItem.Policy);
            return (Func<TSource, TTarget>)cacheItem.Delegate;
        }

        // 缓存未命中：编译委托并缓存（带策略）
        var delegateInstance = CreateCreateMapperDelegate<TSource, TTarget>();
        _mapperDelegateCache[key] = (delegateInstance, DateTime.Now, actualPolicy);

        return delegateInstance;
    }

    private static Action<TSource, TTarget> GetOrCreateUpdateDelegate<TSource, TTarget>()
        where TSource : class
        where TTarget : class
    {
        var key = (typeof(TSource), typeof(TTarget));

        // 缓存命中：更新访问时间
        if (_mapperDelegateCache.TryGetValue(key, out var cacheItem))
        {
            _mapperDelegateCache[key] = (cacheItem.Delegate, DateTime.Now, cacheItem.Policy);
            return (Action<TSource, TTarget>)cacheItem.Delegate;
        }

        // 缓存未命中：编译委托并缓存（使用默认策略）
        var delegateInstance = CreateUpdateMapperDelegate<TSource, TTarget>();
        _mapperDelegateCache[key] = (delegateInstance, DateTime.Now, CachePolicy.Default);

        return delegateInstance;
    }

    private static Func<TSource, TTarget> CreateCreateMapperDelegate<TSource, TTarget>()
        where TSource : class
        where TTarget : class, new()
    {
        var sourceParam = Expression.Parameter(typeof(TSource), "source");
        var targetNew = Expression.New(typeof(TTarget));
        var bindings = new List<MemberBinding>();

        var targetProps = GetCachedProperties<TTarget>();
        var sourceProps = GetCachedProperties<TSource>();

        foreach (var targetProp in targetProps)
        {
            if (!targetProp.CanWrite) continue;

            var sourceProp = sourceProps.FirstOrDefault(p =>
                string.Equals(p.Name, targetProp.Name, StringComparison.OrdinalIgnoreCase) &&
                (p.PropertyType == targetProp.PropertyType ||
                 Nullable.GetUnderlyingType(targetProp.PropertyType) == p.PropertyType));

            if (sourceProp == null || !sourceProp.CanRead) continue;

            // 构建：source.Prop != null ? source.Prop : default
            var sourceAccess = Expression.Property(sourceParam, sourceProp);
            var notNullCheck = Expression.NotEqual(sourceAccess, Expression.Constant(null, sourceProp.PropertyType));
            var conditionalValue = Expression.Condition(notNullCheck, sourceAccess, Expression.Default(targetProp.PropertyType));

            bindings.Add(Expression.Bind(targetProp, conditionalValue));
        }

        var memberInit = Expression.MemberInit(targetNew, bindings);
        var lambda = Expression.Lambda<Func<TSource, TTarget>>(memberInit, sourceParam);

        return lambda.Compile();
    }

    private static Action<TSource, TTarget> CreateUpdateMapperDelegate<TSource, TTarget>()
        where TSource : class
        where TTarget : class
    {
        var sourceParam = Expression.Parameter(typeof(TSource), "source");
        var targetParam = Expression.Parameter(typeof(TTarget), "target");
        var assignExpressions = new List<Expression>();

        var targetProps = GetCachedProperties<TTarget>();
        var sourceProps = GetCachedProperties<TSource>();

        foreach (var targetProp in targetProps)
        {
            if (!targetProp.CanWrite) continue;

            var sourceProp = sourceProps.FirstOrDefault(p =>
                string.Equals(p.Name, targetProp.Name, StringComparison.OrdinalIgnoreCase) &&
                (p.PropertyType == targetProp.PropertyType ||
                 Nullable.GetUnderlyingType(targetProp.PropertyType) == p.PropertyType));

            if (sourceProp == null || !sourceProp.CanRead) continue;

            // 构建：if (source.Prop != null) target.Prop = source.Prop
            var sourceAccess = Expression.Property(sourceParam, sourceProp);
            var targetAccess = Expression.Property(targetParam, targetProp);
            var notNullCheck = Expression.NotEqual(sourceAccess, Expression.Constant(null, sourceProp.PropertyType));
            var assign = Expression.Assign(targetAccess, sourceAccess);
            var conditionalAssign = Expression.Condition(notNullCheck, assign, Expression.Empty());

            assignExpressions.Add(conditionalAssign);
        }

        var block = Expression.Block(assignExpressions);
        var lambda = Expression.Lambda<Action<TSource, TTarget>>(block, sourceParam, targetParam);

        return lambda.Compile();
    }
    #endregion

    #region 内部核心：属性缓存
    private static PropertyInfo[] GetCachedProperties<T>(CachePolicy policy = null)
    {
        var type = typeof(T);
        var actualPolicy = policy ?? CachePolicy.Default;

        // 缓存命中：更新访问时间
        if (_propertyCache.TryGetValue(type, out var cacheItem))
        {
            _propertyCache[type] = (cacheItem.Props, DateTime.Now, cacheItem.Policy);
            return cacheItem.Props;
        }

        // 缓存未命中：获取属性并缓存（带策略）
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !p.GetIndexParameters().Any())
            .ToArray();

        _propertyCache[type] = (properties, DateTime.Now, actualPolicy);

        return properties;
    }
    #endregion

    #region 内部核心：缓存清理逻辑
    private static void OnCacheCleanupTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (!_cleanupLock.TryEnterWriteLock(TimeSpan.FromSeconds(5)))
        {
            Console.WriteLine("缓存清理：获取写锁超时，跳过本次清理");
            return;
        }

        try
        {
            var now = DateTime.Now;
            int propertyCacheCleanCount = 0;
            int delegateCacheCleanCount = 0;

            // 1. 清理过期的属性缓存
            var expiredPropertyKeys = _propertyCache
                .Where(kv => !kv.Value.Policy.NeverExpire &&
                             kv.Value.LastAccessTime.Add(kv.Value.Policy.ExpirationTime) < now)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredPropertyKeys)
            {
                if (_propertyCache.TryRemove(key, out _))
                    propertyCacheCleanCount++;
            }

            // 2. 清理过期的委托缓存
            var expiredDelegateKeys = _mapperDelegateCache
                .Where(kv => !kv.Value.Policy.NeverExpire &&
                             kv.Value.LastAccessTime.Add(kv.Value.Policy.ExpirationTime) < now)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredDelegateKeys)
            {
                if (_mapperDelegateCache.TryRemove(key, out _))
                    delegateCacheCleanCount++;
            }

            if (propertyCacheCleanCount > 0 || delegateCacheCleanCount > 0)
            {
                Console.WriteLine($"缓存清理完成：属性缓存清理{propertyCacheCleanCount}个，委托缓存清理{delegateCacheCleanCount}个");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"缓存清理异常：{ex.Message}");
        }
        finally
        {
            if (_cleanupLock.IsWriteLockHeld)
                _cleanupLock.ExitWriteLock();
        }
    }
    #endregion
}