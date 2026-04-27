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
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机：管理领域元数据、全局配置及业务执行上下文的入口。
/// </summary>
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public static DomainHost<TUserInfo>? Root { get; private set; }

    /// <summary>
    /// 全局根容器（通常为 Singleton 级），仅作为保底解析使用。
    /// </summary>
    public IServiceProvider? ServiceProvider { get; private set; }

    public DomainOptions Options { get; }
    public IConfiguration? Configuration { get; private set; }
    public DomainUserHelperBase<TUserInfo> UserHelper { get; }

    public ILoggerFactory LoggerFactory { get; internal set; } = new NullLoggerFactory();
    public DefaultExceptionLoggerFactory? ExceptionLoggerFactory { get; internal set; }

    private readonly List<DomainFilterAttribute<TUserInfo>> _GlobalFilters = [];
    public IReadOnlyList<DomainFilterAttribute<TUserInfo>> GlobalFilters => _GlobalFilters.AsReadOnly();

    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();
    public IProjectMetaContext? ProjectMetaContext { get; internal set; }
    private ISessionManager<TUserInfo>? _SessionManager;
    /// <summary>
    /// 领域会话管理器（内部属性，不再通过 sp 动态解析）
    /// </summary>
    internal ISessionManager<TUserInfo> SessionManager => _SessionManager
            ?? throw new InvalidOperationException("DomainHost 尚未绑定 ServiceProvider。");

    #region [ 初始化与绑定 ]

    public static DomainHost<TUserInfo> Initialize<TDomainInitializer>(
        IServiceCollection services,
        DomainOptions options,
        IConfiguration? configuration = null)
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        if (Root != null) throw new InvalidOperationException("DomainHost 已重初始化");

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

    internal void BindServiceProvider(IServiceProvider sp)
    {
        ServiceProvider = sp;
        LoggerFactory = sp.GetService<ILoggerFactory>() ?? new NullLoggerFactory();

        // 关键：在绑定时完成唯一一次解析
        // 此时 DI 会自动处理 SessionManager 内部的所有依赖（如缓存服务、配置等）
        _SessionManager = sp.GetRequiredService<ISessionManager<TUserInfo>>();
        // 在绑定时，尝试从容器获取元数据上下文（由初始化器注入）
        ProjectMetaContext = sp.GetService<IProjectMetaContext>();
    }

    #endregion

    #region [ 会话与用户调度 ]

    /// <summary>
    /// 获取或还原用户：优先尝试从 SessionKey 恢复，失败则返回新创建的游客。
    /// 供 Web 中间件使用，确保用户始终存在。
    /// </summary>
    internal async Task<DomainUser<TUserInfo>> GetOrRestoreUserAsync(IServiceProvider sp, string? sessionKey)
    {
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            var session = await SessionManager.TryGetAndActiveSessionAsync(sessionKey);
            if (session?.User != null) return session.User;
        }

        // 自动降级为游客
        var guestSession = await CreateGuestUserAsync(sp);
        return guestSession.User!;
    }

    /// <summary>
    /// 显式创建新的游客会话
    /// </summary>
    internal async Task<SessionInfo<TUserInfo>> CreateGuestUserAsync(IServiceProvider sp)
    {
        var session = await SessionManager.NewSessionAsync();
        var guestSession = await UserHelper.CreateNewGuestSessionAsync(session);

        SessionManager.OnSessionCreated(guestSession);
        return guestSession;
    }

    #endregion

    #region [ 执行上下文入口 ]

    /// <summary>
    /// AOP 拦截上下文创建
    /// </summary>
    internal DomainContext<TUserInfo> NewDomainContext(InvocationContext invocation, IServiceProvider sp)
    {
        var currentUser = ((DomainServiceBase<TUserInfo>)invocation.Target).User;

        // 确保拦截器内部的异步流解析正常
        DomainUser<TUserInfo>.BindScope(sp);

        return new DomainContext<TUserInfo>(
            currentUser,
            invocation,
            _ControllerContracts.GetOrAdd($"{invocation.Target.GetType().FullName}:{invocation.MethodName}", _ => new DomainContracts<TUserInfo>()),
            sp,
            LoggerFactory);
    }

    /// <summary>
    /// 开启一个自动管理生命周期的业务作用域（适配 CLI/Job/Test）。
    /// </summary>
    public async Task<DomainSessionScope<TUserInfo>> CreateSessionScopeAsync(string? sessionKey = null)
    {
        if (ServiceProvider == null) throw new InvalidOperationException("DomainHost 尚未完成初始化绑定。");

        var scope = ServiceProvider.CreateScope();
        try
        {
            // 关键：在获取用户前必须先绑定 Scope，因为恢复 User 可能需要解析数据库服务
            DomainUser<TUserInfo>.BindScope(scope.ServiceProvider);

            var user = await GetOrRestoreUserAsync(scope.ServiceProvider, sessionKey);
            return new DomainSessionScope<TUserInfo>(scope, user);
        }
        catch
        {
            // 异常时物理释放并清理
            DomainUser<TUserInfo>.UnBindScope();
            scope.Dispose();
            throw;
        }
    }
    /// <summary>
    /// 开启一个业务作用域（重用已有的 ServiceProvider）。
    /// 适配：Web 中间件（传入 context.RequestServices）。
    /// </summary>
    public async Task<DomainSessionScope<TUserInfo>> BeginSessionScopeAsync(IServiceProvider sp, string? sessionKey = null)
    {
        // 绑定并获取用户
        DomainUser<TUserInfo>.BindScope(sp);
        var user = await GetOrRestoreUserAsync(sp, sessionKey);

        // 返回“不拥有所有权”的作用域，DisposeAsync 时仅 UnBind 不物理释放 sp
        return new DomainSessionScope<TUserInfo>(sp, user);
    }

    #endregion

    #region [ 过滤器管理 ]

    internal void AddGlobalFilter(DomainFilterAttribute<TUserInfo>? filter)
    {
        if (filter == null || _GlobalFilters.Any(f => f.GetType() == filter.GetType())) return;
        _GlobalFilters.Add(filter);
    }

    #endregion
}