using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.TKWConfig;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using xCodeGen.Abstractions.Metadata;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TKW.Framework.Domain;

public class DomainUser<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    internal static readonly AsyncLocal<IServiceProvider?> _ActiveScope = new();

    // 性能优化核心：缓存构造函数工厂，避免重复反射
    private static readonly ConcurrentDictionary<Type, ObjectFactory> _FactoryCache = new();

    [JsonConstructor]
    public DomainUser() { }

    public string SessionKey { get; set; } = string.Empty;
    public bool IsAuthenticated { get; internal set; }

    [JsonIgnore]
    internal DomainHost<TUserInfo>? AttachedHost { get; set; }

    [JsonIgnore]
    private DomainHost<TUserInfo> Host => AttachedHost
        ?? DomainHost<TUserInfo>.Root
        ?? throw new InvalidOperationException("DomainUser 尚未关联到有效的 DomainHost 实例。");

    [JsonIgnore]
    private IServiceProvider ServiceProvider => _ActiveScope.Value ?? Host.ServiceProvider
        ?? throw new InvalidOperationException("无法获取解析作用域。");

    [JsonInclude]
    public TUserInfo UserInfo { get; set; } = null!;

    #region 服务解析 (设计思路回归：通过 this 控制初始化)

    /// <summary>
    /// 核心控制点：强制通过当前 User 实例作为参数来初始化服务
    /// 使用预编译工厂缓存，消除反射性能开销
    /// </summary>
    public TDomainService Use<TDomainService>() where TDomainService : IDomainService
    {
#if DEBUG
        // 守卫：防止在领域层内部不小心绕过 AOP 直接解析控制器类
        if (typeof(IAopContract).IsAssignableFrom(typeof(TDomainService)))
            throw new InvalidOperationException(
                $"[V4 架构守卫] 检测到直接解析控制器类 {typeof(TDomainService).Name}。请改用 UseAop<接口>()。");
#endif
        var type = typeof(TDomainService);
        var factory = _FactoryCache.GetOrAdd(type, t =>
            ActivatorUtilities.CreateFactory(t, [typeof(DomainUser<TUserInfo>)]));

        return (TDomainService)factory(ServiceProvider, [this]);
    }

    /// <summary>
    /// 解析 AOP 契约服务
    /// </summary>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        // 核心限制：UseAop 必须针对接口调用，否则无法解析出装饰器代理
        if (!typeof(TAopContract).IsInterface)
            throw new InvalidOperationException(
                $"[V4 架构守卫] UseAop 仅支持解析接口契约。若要解析实现类 {typeof(TAopContract).Name}，请改用接口。");

        // AOP 装饰器已在 DI 注册到接口，直接解析即可获得代理实例
        return ServiceProvider.GetRequiredService<TAopContract>();
    }

    /// <summary>
    /// 解除绑定（清理上下文）
    /// </summary>
    public static void UnBindScope() => _ActiveScope.Value = null;

    /// <summary>
    /// 绑定当前异步执行流的解析作用域
    /// </summary>
    public static void BindScope(IServiceProvider? provider) => _ActiveScope.Value = provider;

    /// <summary>
    /// 获取当前执行流中的 ServiceProvider
    /// </summary>
    public static IServiceProvider? GetCurrentScope() => _ActiveScope.Value;

    public ILogger CreateLogger(string categoryName) => Host.LoggerFactory.CreateLogger(categoryName);
    public ILogger<T> CreateLogger<T>() => Host.LoggerFactory.CreateLogger<T>();

    #endregion

    #region AOP 上下文透传
    /// <summary>
    /// 获取当前正在执行的方法的元数据信息（通过 AOP 上下文获取），在 IProjectMetaContext 中查询
    /// </summary>
    public MethodMetadata? GetCurrentMethodMeta()
    {
        var call = this.CurrentInvocation;
        if (call == null || Host.ProjectMetaContext == null) return null;

        // 通过接口调用，解决命名空间问题
        return Host.ProjectMetaContext.GetMethodMeta(call.Target.GetType().FullName, call.MethodName);
    }

    /// <summary>
    /// 获取当前正在执行的 AOP 领域上下文
    /// </summary>
    [JsonIgnore]
    public DomainContext<TUserInfo>? CurrentContext => StaticDomainInterceptor<TUserInfo>.CurrentContext;

    /// <summary>
    /// 获取当前拦截的方法、参数等详细信息
    /// </summary>
    [JsonIgnore]
    public InvocationContext? CurrentInvocation => CurrentContext?.Invocation;

    #endregion

    #region 业务逻辑 (保持不变)
    public bool IsInRole<T>(T role) where T : struct, Enum => UserInfo.IsInRole(role);
    public bool IsInRole(string role) => UserInfo.IsInRole(role);

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserInfo.UserIdString),
            new(ClaimTypes.Name, UserInfo.UserName),
            new(ClaimTypes.GivenName, UserInfo.DisplayName),
            new("SessionKey", SessionKey),
            new("LoginFrom", UserInfo.LoginFrom.ToString()),
            new("IsAuthenticated", IsAuthenticated.ToString().ToLowerInvariant()),
        };
        claims.AddRange(UserInfo.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TKW.Domain", ClaimTypes.Name, ClaimTypes.Role));
    }

    public async Task LoginAsUserAsync(string userName, string passwordHashed, EnumLoginFrom loginFrom)
    {
        var userInfo = await Host.UserHelper.UserLoginAsync(this, userName, passwordHashed, loginFrom);
        UserInfo = userInfo;
        IsAuthenticated = true;
        await UpdateAndActiveSessionAsync();
    }

    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var session = await Host.SessionManager!.GetSessionAsync(SessionKey);
        var newSession = session.BindUser(this);
        await Host.SessionManager.UpdateAndActiveSessionAsync(newSession.Key, _ => newSession);
        return newSession;
    }

    public IDomainUnitOfWork GetUow()
    {
        var manager = ServiceProvider.GetRequiredService<IDomainUnitOfWorkManager>();
        return manager.GetUnitOfWork();
    }
    #endregion

    #region 配置项访问
    public string? TryGetOption(string keyName)
    {
        Host.Options.ConfigDictionary.TryGetValue(keyName, out var value);
        return value;
    }
    public string GetRequiredOption(string keyName) => TryGetOption(keyName) ?? throw new ConfigurationErrorException(keyName);
    #endregion
}