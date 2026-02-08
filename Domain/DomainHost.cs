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
using System.Reflection;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机类，用于初始化和管理领域服务（整个框架的统一入口）
/// </summary>
/// <remarks>
/// 负责容器构建、用户上下文、会话管理、全局 Filter 注册等核心基础设施。
/// 全局过滤器现在统一由本类管理，支持运行时添加和环境差异化配置。
/// </remarks>
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 静态根实例，确保整个应用只有一个 DomainHost 实例
    /// </summary>
    public static DomainHost<TUserInfo>? Root { get; private set; }

    /// <summary>
    /// 全局过滤器列表（实例级别，默认为空）
    /// 通过 AddGlobalFilter / EnableXXX 方法显式添加，支持按环境启用
    /// </summary>
    private readonly List<DomainFilterAttribute<TUserInfo>> _GlobalFilters = [];

    /// <summary>
    /// 只读全局过滤器集合，供 DomainInterceptor 使用
    /// </summary>
    public IReadOnlyList<DomainFilterAttribute<TUserInfo>> GlobalFilters => _GlobalFilters.AsReadOnly();

    /// <summary>
    /// 添加单个全局过滤器（推荐在 OnContainerBuilt 或扩展方法中调用）
    /// </summary>
    internal void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        // 避免完全相同的实例重复添加
        if (_GlobalFilters.Contains(filter))
        {
            return;
        }

        // 可选：再加一层类型检查（防止不同参数的同类型 Filter 重复）
        if (_GlobalFilters.Any(f => f.GetType() == filter.GetType()))
        {
            // 可以选择记录日志或抛异常
            // _domainHost.LoggerFactory?.CreateLogger<DomainHost<TUserInfo>>()
            //     .LogWarning("尝试重复添加相同类型的全局过滤器: {FilterType}", filter.GetType().Name);
            return;
        }
        _GlobalFilters.Add(filter);
    }

    /// <summary>
    /// 批量添加全局过滤器
    /// </summary>
    internal void AddGlobalFilters(IEnumerable<DomainFilterAttribute<TUserInfo>> filters)
    {
        _GlobalFilters.AddRange(filters);
    }

    /// <summary>
    /// 初始化 DomainHost 的静态入口方法
    /// </summary>
    public static DomainHost<TUserInfo> Build<TDomainInitializer, TDomainHelper, TSessionManager>(
        ContainerBuilder? upLevelContainer = null,
        IConfiguration? configuration = null)
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo, TDomainHelper>, new()
        where TDomainHelper : DomainHelperBase<TUserInfo>
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        if (Root != null)
            throw new InvalidOperationException("不能重复初始化 DomainHost");

        var isExternalContainer = upLevelContainer != null;
        var containerBuilder = upLevelContainer ?? new ContainerBuilder();

        // 注册核心组件
        containerBuilder.RegisterType<DomainContext<TUserInfo>>();
        containerBuilder.RegisterType<DomainInterceptor<TUserInfo>>();

        // 注册默认全局异常工厂（可被应用层替换）
        containerBuilder.RegisterType<DefaultDomainGlobalExceptionFactory>().AsSelf();

        // 注册日志工厂（默认 NullLoggerFactory，可被表现层覆盖）
        containerBuilder.UseLogger(new NullLoggerFactory()).As<ILoggerFactory>();

        // 执行应用层初始化
        IServiceCollection services = new ServiceCollection();
        var initializer = new TDomainInitializer();
        var userHelper = initializer.InitializeDiContainer(containerBuilder, services, configuration);

        // 注册会话管理器
        containerBuilder.UseSessionManager<TSessionManager, TUserInfo>();

        // 创建 DomainHost 实例
        Root = new DomainHost<TUserInfo>(userHelper, containerBuilder, services, configuration, isExternalContainer);
        containerBuilder.Register(_ => Root);

        // 外部容器场景：构建完成后回调
        if (isExternalContainer)
        {
            containerBuilder.RegisterBuildCallback(context =>
            {
                if (Root == null) return;

                if (context is IContainer container)
                    Root.Container = container;
                else
                {
                    try
                    {
                        var maybe = context.ResolveOptional<IContainer>();
                        if (maybe != null) Root.Container = maybe;
                    }
                    catch { /* 忽略，保持容器继续启动 */ }
                }

                if (Root.Container != null)
                {
                    initializer.ContainerBuiltCallback(Root, isExternalContainer);

                    // 更新日志工厂（优先使用外部注册的实现）
                    if (Root.Container.IsRegistered<ILoggerFactory>())
                        Root.LoggerFactory = Root.Container.Resolve<ILoggerFactory>();
                }
            });
        }

        return Root;
    }

    /// <summary>
    /// 私有构造函数，用于初始化DomainHost实例
    /// </summary>
    /// <param name="userHelper"></param>
    /// <param name="containerBuilder">Autofac容器构建器</param>
    /// <param name="services">兼容第三方组件的依赖注入的服务集合</param>
    /// <param name="configuration">配置项</param>
    /// <param name="isExternalContainer"></param>
    private DomainHost(DomainHelperBase<TUserInfo> userHelper, ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration = null, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(userHelper);
        UserHelper = userHelper;
        IsIsExternalContainer = isExternalContainer;

        // 断言containerBuilder不为空
        containerBuilder.EnsureNotNull(name: nameof(containerBuilder));

        // 兼容第三方组件的依赖注入的服务集合
        containerBuilder.Populate(services);

        // 配置项
        Configuration = configuration;

        // 构建Autofac容器
        if (!isExternalContainer)
            Container = containerBuilder.Build();
    }

    public DomainHelperBase<TUserInfo> UserHelper { get; }

    public IConfiguration? Configuration { get; private set; }

    /// <summary>
    /// 日志工厂（永远非 null，默认使用 NullLoggerFactory）
    /// </summary>
    public ILoggerFactory LoggerFactory { get; private set; } = new NullLoggerFactory();

    public IContainer? Container { get; private set; }

    internal ISessionManager<TUserInfo>? SessionManager => field ??= Container?.Resolve<ISessionManager<TUserInfo>>();

    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();

    public bool IsIsExternalContainer { get; private set; }

    /// <summary>
    /// 创建新的领域上下文（每次 AOP 方法调用时执行）
    /// </summary>
    internal DomainContext<TUserInfo> NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
    {
        // 处理控制器级 Filters + Flags
        var interfaceType = invocation.TargetType.GetInterfaces()
            .First(i => !i.Name.StartsWith("IDomainService", StringComparison.OrdinalIgnoreCase));

        var key = $"{interfaceType.FullName}";
        if (!_ControllerContracts.TryGetValue(key, out var interfaceContract))
        {
            interfaceContract = new DomainContracts<TUserInfo>();
            var filters = interfaceType.GetCustomAttributes<DomainFilterAttribute<TUserInfo>>(true);
            var flags = interfaceType.GetCustomAttributes<DomainFlagAttribute>(true);

            foreach (var filter in filters) interfaceContract.ControllerFilters.Add(filter);
            foreach (var flag in flags) interfaceContract.ControllerFlags.Add(flag);
            _ControllerContracts.TryAdd(key, interfaceContract);
        }

        // 处理方法级 Filters + Flags
        key = $"{invocation.TargetType.FullName}:{invocation.Method.Name}";
        if (!_ControllerContracts.TryGetValue(key, out var methodContract))
        {
            methodContract = new DomainContracts<TUserInfo>();
            var method = invocation.Method;
            var filters = method.GetCustomAttributes<DomainFilterAttribute<TUserInfo>>(true);
            var flags = method.GetCustomAttributes<DomainFlagAttribute>(true);

            foreach (var filter in filters)
                methodContract.MethodFilters.Add(filter);
            foreach (var filter in interfaceContract.ControllerFilters)
                methodContract.ControllerFilters.Add(filter);

            foreach (var flag in flags)
                methodContract.MethodFlags.Add(flag);
            foreach (var flag in interfaceContract.ControllerFlags)
                methodContract.ControllerFlags.Add(flag);

            _ControllerContracts.TryAdd(key, methodContract);
        }

        // 注入 LoggerFactory（已确保非 null）
        var context = lifetimeScope.Resolve<DomainContext<TUserInfo>>(
            TypedParameter.From(((DomainServiceBase<TUserInfo>)invocation.InvocationTarget).User),
            TypedParameter.From(invocation),
            TypedParameter.From(methodContract),
            TypedParameter.From(LoggerFactory));

        return context;
    }

    public static Func<DomainHost<TUserInfo>> Factory => () => Root!;

    #region 会话相关方法

    public async Task<SessionInfo<TUserInfo>> NewGuestSessionAsync()
    {
        var session = await SessionManager!.NewSessionAsync();
        var newSession = await UserHelper.CreateNewGuestSessionAsync(session).ConfigureAwait(false);
        SessionManager.OnSessionCreated(newSession);
        return newSession;
    }

    internal async Task<DomainUser<TUserInfo>> GetDomainUserAsync(string sessionKey)
    {
        var session = await SessionManager!.GetAndActiveSessionAsync(sessionKey).ConfigureAwait(false);
        return session.User!;
    }

    #endregion
}