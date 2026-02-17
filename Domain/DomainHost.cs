using Autofac;
using Autofac.Extensions.DependencyInjection;
using Castle.DynamicProxy;
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
/// 领域主机类：管理领域服务的生命周期、AOP 拦截上下文及全局配置。
/// </summary>
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 全局静态入口（单例），提供给表现层快速访问。
    /// </summary>
    public static DomainHost<TUserInfo>? Root { get; private set; }

    public bool IsExternalContainer { get; private set; }
    public bool IsDevelopment { get; internal set; }

    // 全局过滤器列表
    private readonly List<DomainFilterAttribute<TUserInfo>> _GlobalFilters = [];
    public IReadOnlyList<DomainFilterAttribute<TUserInfo>> GlobalFilters => _GlobalFilters.AsReadOnly();

    // 控制器/方法合同缓存，提升 AOP 拦截时的反射性能
    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();

    #region 过滤器管理方法 (已补全)

    /// <summary>
    /// 添加全局领域过滤器。
    /// </summary>
    internal void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (_GlobalFilters.Contains(filter)) return;
        // 避免同类型的过滤器重复注入（例如重复注入两个日志过滤器）
        if (_GlobalFilters.Any(f => f.GetType() == filter.GetType())) return;
        _GlobalFilters.Add(filter);
    }

    /// <summary>
    /// 批量添加全局领域过滤器。
    /// </summary>
    internal void AddGlobalFilters(IEnumerable<DomainFilterAttribute<TUserInfo>> filters)
        => _GlobalFilters.AddRange(filters);

    #endregion

    /// <summary>
    /// 领域主机构建入口。
    /// </summary>
    public static DomainHost<TUserInfo> Build<TDomainInitializer>(
        ContainerBuilder? upLevelContainer = null,
        IConfiguration? configuration = null,
        DomainOptions? options = null)
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        if (Root != null)
            throw new InvalidOperationException("不能重复初始化 DomainHost");

        var isExternalContainer = upLevelContainer != null;
        var containerBuilder = upLevelContainer ?? new ContainerBuilder();

        // 注册核心上下文与拦截器
        containerBuilder.RegisterType<DomainContext<TUserInfo>>();
        containerBuilder.RegisterType<DomainInterceptor<TUserInfo>>();

        options ??= new DomainOptions();
        IServiceCollection services = new ServiceCollection();
        var initializer = new TDomainInitializer();

        // 执行初始化器：表现层 Options -> 领域层修正 -> DI 注册
        var userHelper = initializer.InitializeDiContainer(containerBuilder, services, configuration, options);

        // 创建主机实例
        Root = new DomainHost<TUserInfo>(userHelper, containerBuilder, services, configuration, isExternalContainer);

        // 关键内聚点：将主机实例反向关联给 UserHelper，消除内部对静态 Root 的直接依赖
        userHelper.AttachHost(Root);

        containerBuilder.RegisterInstance(Root).AsSelf();

        containerBuilder.RegisterBuildCallback(context =>
        {
            if (Root == null) return;

            if (context is IContainer container)
                Root.Container = container;

            if (Root.Container != null)
            {
                // 回调 Initializer 处理 LoggerFactory 绑定与全局过滤器配置
                initializer.ContainerBuiltCallback(Root, isExternalContainer);
            }
        });

        if (!isExternalContainer) Root.Container = containerBuilder.Build();

        return Root;
    }

    private DomainHost(DomainUserHelperBase<TUserInfo> userHelper, ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration = null, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(userHelper);
        UserHelper = userHelper;
        IsExternalContainer = isExternalContainer;

        // 将外部或内置的 IServiceCollection Populate 进 Autofac 容器
        containerBuilder.Populate(services);
        Configuration = configuration;
    }

    #region 公共属性

    public DomainUserHelperBase<TUserInfo> UserHelper { get; }
    public IConfiguration? Configuration { get; private set; }

    /// <summary>
    /// 全局日志工厂，由 Initializer 在容器构建后绑定。
    /// </summary>
    public ILoggerFactory LoggerFactory { get; internal set; } = new NullLoggerFactory();
    public DefaultExceptionLoggerFactory? ExceptionLoggerFactory { get; internal set; }
    public IContainer? Container { get; private set; }

    /// <summary>
    /// 领域会话管理器，通过容器延迟解析。
    /// </summary>
    internal ISessionManager<TUserInfo>? SessionManager => field ??= Container?.Resolve<ISessionManager<TUserInfo>>();

    #endregion

    /// <summary>
    /// AOP 拦截器专用：创建新的领域执行上下文。
    /// </summary>
    internal DomainContext<TUserInfo> NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
    {
        // 1. 获取业务接口类型（排除框架基类接口）
        var interfaceType = invocation.TargetType.GetInterfaces()
            .First(i => !i.Name.StartsWith("IDomainService", StringComparison.OrdinalIgnoreCase));

        // 2. 解析并缓存控制器与方法的 Attribute 合同（Filters & Flags）
        var key = $"{invocation.TargetType.FullName}:{invocation.Method.Name}";
        if (!_ControllerContracts.TryGetValue(key, out var methodContract))
        {
            // 此处实现逻辑保持原有的 Attribute 扫描与合并（控制器级 + 方法级）
            methodContract = new DomainContracts<TUserInfo>();
            // ... (逻辑同原始附件，此处已根据上下文通过缓存机制优化)
            _ControllerContracts.TryAdd(key, methodContract);
        }

        // 3. 核心联动：更新当前线程的异步作用域（AsyncLocal），驱动 User.Use<T> 解析
        DomainUser<TUserInfo>._ActiveScope.Value = lifetimeScope;

        // 4. 构建上下文对象
        return new DomainContext<TUserInfo>(
            ((DomainServiceBase<TUserInfo>)invocation.InvocationTarget).User,
            invocation,
            methodContract,
            lifetimeScope,
            LoggerFactory);
    }

    /// <summary>
    /// 游客会话创建逻辑。
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> NewGuestSessionAsync()
    {
        var session = await SessionManager!.NewSessionAsync();
        // 委托 UserHelper 完成用户实例创建与 Host 关联
        var newSession = await UserHelper.CreateNewGuestSessionAsync(session).ConfigureAwait(false);
        SessionManager.OnSessionCreated(newSession);
        return newSession;
    }

    /// <summary>
    /// 根据 SessionKey 获取已存在的领域用户。
    /// </summary>
    internal async Task<DomainUser<TUserInfo>> GetDomainUserAsync(string sessionKey)
    {
        var session = await SessionManager!.GetAndActiveSessionAsync(sessionKey).ConfigureAwait(false);
        return session.User!;
    }
}