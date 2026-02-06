using Autofac;
using Autofac.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机类，用于初始化和管理领域服务
/// </summary>
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 静态根实例，确保只有一个DomainHost实例
    /// </summary>
    public static DomainHost<TUserInfo>? Root { get; private set; }

    /// <summary>
    /// 初始化DomainHost的静态方法
    /// </summary>
    /// <typeparam name="TDomainHelper">用户助手类型</typeparam>
    /// <typeparam name="TSessionManager"></typeparam>
    /// <typeparam name="TUserInfo"></typeparam>
    /// <typeparam name="TDomainInitializer"></typeparam>
    /// <param name="upLevelContainer">上级容器：例如 ASP.NET/MAUI 原生容器的 Autofac 版本</param>
    /// <param name="configuration">配置项</param>
    /// <returns>DomainHost实例</returns>
    public static DomainHost<TUserInfo> Build<TDomainInitializer, TDomainHelper, TSessionManager>(
        ContainerBuilder? upLevelContainer = null,
        IConfiguration? configuration = null)
        where TDomainInitializer: DomainHostInitializerBase<TUserInfo, TDomainHelper>, new()
        where TDomainHelper : DomainHelperBase<TUserInfo>
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        // 确保不能重复初始化
        if (Root != null) throw new InvalidOperationException("不能重复初始化 DomainHost");

        var containerBuilder = upLevelContainer ?? new ContainerBuilder();
        if (upLevelContainer != null)
        {
            // 注册 IStartable: 在容器构建时自动回填 DomainHost
            containerBuilder.RegisterType<TDomainInitializer>()
                .As<IStartable>()
                .SingleInstance();
        }

        containerBuilder.RegisterType<DomainContext<TUserInfo>>();
        containerBuilder.RegisterType<DomainInterceptor<TUserInfo>>(); // 注册领域框架拦截器

        // 执行委托方法，获取用户助手实例
        IServiceCollection services = new ServiceCollection();
        var initializer = new TDomainInitializer();
        var userHelper = initializer.InitializeDiContainer(containerBuilder, services, configuration);
        // containerBuilder.RegisterInstance(userHelper).As<TDomainHelper>();  // 可通过 DomainHost 获得，不需要通过容器

        // 构造并注入 ISessionManager 实例，需在 configureServices() 方法中注入依赖的类型或实例，如 HybridCache
        containerBuilder.UseSessionManager<TUserInfo>();
        // 构造 DomainHost 实例，通过全局唯一的单例获取效率更高
        Root = new DomainHost<TUserInfo>(userHelper, containerBuilder, services, configuration, upLevelContainer == null);
        containerBuilder.Register(_ => Root);   // 将类厂注入容器
        return Root;
    }

    /// <summary>
    /// 私有构造函数，用于初始化DomainHost实例
    /// </summary>
    /// <param name="userHelper"></param>
    /// <param name="containerBuilder">Autofac容器构建器</param>
    /// <param name="services">兼容第三方组件的依赖注入的服务集合</param>
    /// <param name="configuration">配置项</param>
    /// <param name="externalContainer"></param>
    private DomainHost(DomainHelperBase<TUserInfo> userHelper, ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration = null, bool externalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(userHelper);
        UserHelper = userHelper;
        // 断言containerBuilder不为空
        containerBuilder.EnsureNotNull(name: nameof(containerBuilder));

        // 兼容第三方组件的依赖注入的服务集合
        containerBuilder.Populate(services);
        
        // 配置项
        Configuration = configuration;

        // 构建Autofac容器
        if (!externalContainer)
        {
            Container = containerBuilder.Build();
            // 创建服务提供者
            // ServicesProvider = new AutofacServiceProvider(Container);

            // 获取日志工厂实例，如果为空则使用NullLoggerFactory
            /*
            LoggerFactory = Container.IsRegistered<ILoggerFactory>()
                ? Container.Resolve<ILoggerFactory>()
                : new NullLoggerFactory();
        */
        }
    }

    public DomainHelperBase<TUserInfo> UserHelper { get; }

    /// <summary>
    /// 配置根
    /// </summary>
    public IConfiguration? Configuration { get; private set; }

    /// <summary>
    /// 日志工厂
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; private set; }

    /// <summary>
    /// Autofac容器
    /// </summary>
    public IContainer? Container { get; private set; }

    /// <summary>
    /// SessionManager 实例
    /// </summary>
    internal ISessionManager<TUserInfo>? SessionManager => field ??= Container?.Resolve<ISessionManager<TUserInfo>>();

    /// <summary>
    /// 控制器契约的并发字典缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();

    public void InitializeAfterHostBuild(ILifetimeScope rootScope)
    {
        ArgumentNullException.ThrowIfNull(rootScope);
        if (Container != null) return; // 已初始化则跳过

        // 根作用域通常可以转换为 IContainer
        if (rootScope is IContainer ic)
        {
            Container = ic;
        }
        else
        {
            // 若不是 IContainer，仍把根作用域保留为 ILifetimeScope 的形式
            // 这里将尝试将 ILifetimeScope 转为 IContainer 的能力视为可选：
            Container = rootScope.ResolveOptional<IContainer>()
                        ?? throw new InvalidOperationException("无法回填 IContainer");
        }
        /*ServicesProvider = new AutofacServiceProvider(Container);
        LoggerFactory = Container.IsRegistered<ILoggerFactory>() ? Container.Resolve<ILoggerFactory>() : new NullLoggerFactory();
        */

    }

    /// <summary>
    /// 创建新的领域上下文
    /// </summary>
    /// <param name="invocation">代理调用信息</param>
    /// <param name="lifetimeScope">生命周期范围</param>
    /// <returns>领域上下文实例</returns>
    internal DomainContext<TUserInfo> NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
    {
        // 注意：仅处理接口上定义的Attribute，忽略类的Attribute

        // TODO: 处理全局Filters

        // 处理控制器级Filters
        var interfaceType = invocation.TargetType.GetInterfaces().First(i => !i.Name.StartsWith("IDomainService", StringComparison.OrdinalIgnoreCase));

        var key = $"{interfaceType.FullName}";
        if (_ControllerContracts.TryGetValue(key, out var interfaceContract))
        {
            // 从缓存中获取控制器契约
        }
        else
        {
            interfaceContract = new DomainContracts<TUserInfo>();
            var filters = interfaceType.GetCustomAttributes<DomainActionFilterAttribute<TUserInfo>>(true);
            var flags = interfaceType.GetCustomAttributes<DomainFlagAttribute>(true);

            // 将控制器级Filters和Flags加入到缓存中，减少每次重新查询、获取的负载
            foreach (var filter in filters) interfaceContract.ControllerFilters.Add(filter);
            foreach (var flag in flags) interfaceContract.ControllerFlags.Add(flag);
            _ControllerContracts.TryAdd(key, interfaceContract);
        }

        // 处理方法级Filters
        key = $"{invocation.TargetType.FullName}:{invocation.Method.Name}";
        if (_ControllerContracts.TryGetValue(key, out var methodContract))
        {
            // 从缓存中获取方法契约
        }
        else
        {
            methodContract = new DomainContracts<TUserInfo>();
            var method = invocation.Method;
            var filters = method.GetCustomAttributes<DomainActionFilterAttribute<TUserInfo>>(true);
            var flags = method.GetCustomAttributes<DomainFlagAttribute>(true);

            // 方法级的Filters
            foreach (var filter in filters)
                methodContract.MethodFilters.Add(filter);
            // 控制器级的Filters
            foreach (var filter in interfaceContract.ControllerFilters)
                methodContract.ControllerFilters.Add(filter);

            // 方法级的Flags
            foreach (var flag in flags)
                methodContract.MethodFlags.Add(flag);
            // 控制器级的Flags
            foreach (var flag in interfaceContract.ControllerFlags)
                methodContract.ControllerFlags.Add(flag);

            // 加入缓存中，减少每次重新查询、获取的负载
            _ControllerContracts.TryAdd(key, methodContract);
        }
        // 解析领域上下文实例
        var context = lifetimeScope.Resolve<DomainContext<TUserInfo>>(
            TypedParameter.From(((DomainServiceBase<TUserInfo>)invocation.InvocationTarget).User),
            TypedParameter.From(invocation),
            TypedParameter.From(methodContract));
        return context;
    }

    /// <summary>
    /// 获取DomainHost实例的工厂方法
    /// </summary>
    public static Func<DomainHost<TUserInfo>> Factory => () => Root!;

    #region 会话相关方法

    /// <summary>
    /// 新的游客会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> NewGuestSessionAsync()
    {
        var session = await SessionManager!.NewSessionAsync();
        var newSession = await UserHelper.CreateNewGuestSessionAsync(session)
            .ConfigureAwait(false);
        SessionManager.OnSessionCreated(newSession);
        return newSession;
    }

    /// <summary>
    /// 获取领域用户并更新会话
    /// </summary>
    internal async Task<DomainUser<TUserInfo>> GetDomainUserAsync(string sessionKey)
    {
        var session = await SessionManager!.GetAndActiveSessionAsync(sessionKey)
            .ConfigureAwait(false);

        return session.User!;
    }

    #endregion
}