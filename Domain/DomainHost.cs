using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// V4 领域主机：管理领域服务的生命周期、AOP 拦截上下文及全局配置。
/// 彻底摆脱 Autofac 依赖，拥抱标准 .NET DI。
/// </summary>
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public static DomainHost<TUserInfo>? Root { get; private set; }

    // 替换原本的 IContainer，作为全局解析的保底入口
    public IServiceProvider? ServiceProvider { get; private set; }

    public DomainOptions Options { get; }
    public IConfiguration? Configuration { get; private set; }
    public DomainUserHelperBase<TUserInfo> UserHelper { get; }

    public ILoggerFactory LoggerFactory { get; internal set; } = new NullLoggerFactory();
    public DefaultExceptionLoggerFactory? ExceptionLoggerFactory { get; internal set; }

    private readonly List<DomainFilterAttribute<TUserInfo>> _GlobalFilters = [];
    public IReadOnlyList<DomainFilterAttribute<TUserInfo>> GlobalFilters => _GlobalFilters.AsReadOnly();

    // 控制器/方法合同缓存，提升 AOP 拦截时的反射性能
    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();

    /// <summary>
    /// V4 启动入口：基于标准 IServiceCollection
    /// </summary>
    public static DomainHost<TUserInfo> Initialize<TDomainInitializer>(
        IServiceCollection services,
        DomainOptions options,
        IConfiguration? configuration = null)
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        if (Root != null) throw new InvalidOperationException("DomainHost 已初始化");

        // 注册 V4 静态拦截器和配置
        services.AddSingleton<StaticDomainInterceptor<TUserInfo>>();
        services.AddSingleton(options);

        var initializer = new TDomainInitializer();
        var userHelper = initializer.InitializeDiContainer(services, configuration, options);

        Root = new DomainHost<TUserInfo>(userHelper, options, configuration);
        userHelper.AttachHost(Root);

        return Root;
    }

    private DomainHost(DomainUserHelperBase<TUserInfo> userHelper, DomainOptions options, IConfiguration? configuration)
    {
        UserHelper = userHelper ?? throw new ArgumentNullException(nameof(userHelper));
        Options = options;
        Configuration = configuration;
    }

    /// <summary>
    /// 核心绑定：在 ServiceProvider 构建完成后，由初始化器回调。
    /// </summary>
    internal void BindServiceProvider(IServiceProvider sp)
    {
        ServiceProvider = sp;
        LoggerFactory = sp.GetService<ILoggerFactory>() ?? new NullLoggerFactory();
    }

    /// <summary>
    /// 领域会话管理器，通过 ServiceProvider 延迟解析。
    /// </summary>
    internal ISessionManager<TUserInfo>? SessionManager => ServiceProvider?.GetService<ISessionManager<TUserInfo>>();

    /// <summary>
    /// AOP 驱动：为当前拦截调用创建上下文，并将 IServiceProvider 绑定到 DomainUser。
    /// </summary>
    internal DomainContext<TUserInfo> NewDomainContext(InvocationContext invocation, IServiceProvider sp)
    {
        // 更新当前线程的异步作用域（AsyncLocal），驱动 User.Use<T> 解析
        DomainUser<TUserInfo>._ActiveScope.Value = sp;

        return new DomainContext<TUserInfo>(
            ((DomainServiceBase<TUserInfo>)invocation.Target).User,
            invocation,
            _ControllerContracts.GetOrAdd($"{invocation.Target.GetType().FullName}:{invocation.MethodName}", _ => new DomainContracts<TUserInfo>()),
            sp,
            LoggerFactory);
    }

    #region 全局过滤器管理

    internal void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter)
    {
        if (filter == null || _GlobalFilters.Any(f => f.GetType() == filter.GetType())) return;
        _GlobalFilters.Add(filter);
    }

    internal void AddGlobalFilters(IEnumerable<DomainFilterAttribute<TUserInfo>> filters)
    {
        foreach (var filter in filters) AddGlobalFilter(filter);
    }

    #endregion

    public async Task<SessionInfo<TUserInfo>> NewGuestSessionAsync()
    {
        var session = await SessionManager!.NewSessionAsync();
        var newSession = await UserHelper.CreateNewGuestSessionAsync(session);
        SessionManager.OnSessionCreated(newSession);
        return newSession;
    }

    internal async Task<DomainUser<TUserInfo>> GetDomainUserAsync(string sessionKey)
    {
        var session = await SessionManager!.GetAndActiveSessionAsync(sessionKey);
        return session.User!;
    }
}