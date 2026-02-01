using Autofac;
using Autofac.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机类，用于初始化和管理领域服务
/// </summary>
public sealed class DomainHost
{
    /// <summary>
    /// 静态根实例，确保只有一个DomainHost实例
    /// </summary>
    public static DomainHost Root { get; private set; }

    /// <summary>
    /// 初始化DomainHost的静态方法
    /// </summary>
    /// <typeparam name="TDomainHelper">用户助手类型</typeparam>
    /// <typeparam name="TSessionManager"></typeparam>
    /// <param name="configureServices">配置服务的委托</param>
    /// <param name="upLevelServices">上级服务集合</param>
    /// <returns>DomainHost实例</returns>
    public static DomainHost Initial<TDomainHelper, TSessionManager>(
        Func<ContainerBuilder, IServiceCollection, ConfigurationBuilder, IServiceCollection, TDomainHelper> configureServices,
        IServiceCollection upLevelServices = null)
        where TDomainHelper : DomainHelperBase
    where TSessionManager : ISessionManager
    {
        // 确保不能重复初始化
        if (Root != null) throw new InvalidOperationException("不能重复初始化");

        // configureServices 不为空
        ArgumentNullException.ThrowIfNull(configureServices);
        var configBuilder = new ConfigurationBuilder();
        configBuilder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory); // 设置默认的BasePath

        IServiceCollection services = new ServiceCollection();
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterType<DomainContext>();
        containerBuilder.RegisterType<DomainInterceptor>(); // 注册领域框架拦截器
        containerBuilder.Register(_ => Root);   // 将类厂注入容器

        // 执行委托方法，获取用户助手实例
        var userHelper = configureServices(containerBuilder, services, configBuilder, upLevelServices);
        containerBuilder.RegisterInstance(userHelper).As<TDomainHelper>();  // 可通过 DomainHost 获得，不需要通过容器
        // 构造并注入 ISessionManager 实例，需在 configureServices() 方法中注入依赖的类型或实例，如 HybridCache
        containerBuilder.RegisterType<TSessionManager>().As<ISessionManager>().SingleInstance();

        // 构造 DomainHost 实例，通过全局唯一的单例获取效率更高
        Root = new DomainHost(userHelper, containerBuilder, services, configBuilder);
        return Root;
    }

    /// <summary>
    /// 私有构造函数，用于初始化DomainHost实例
    /// </summary>
    /// <param name="containerBuilder">Autofac容器构建器</param>
    /// <param name="services">服务集合</param>
    /// <param name="configurationBuilder">配置构建器</param>
    /// <param name="userHelper"></param>
    private DomainHost(DomainHelperBase userHelper, ContainerBuilder containerBuilder,
        IServiceCollection services = null, IConfigurationBuilder configurationBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(userHelper);
        UserHelper = userHelper;
        // 断言containerBuilder不为空
        containerBuilder.EnsureNotNull(name: nameof(containerBuilder));

        // 如果services为空，则初始化一个新的服务集合
        services ??= new ServiceCollection();
        containerBuilder.Populate(services);

        // 如果configurationBuilder为空，则初始化一个新的配置构建器
        var configBuilder = configurationBuilder ?? new ConfigurationBuilder();
        containerBuilder.RegisterInstance(Configuration = configBuilder.Build());

        // 构建Autofac容器
        Container = containerBuilder.Build();
        // 创建服务提供者
        ServicesProvider = new AutofacServiceProvider(Container);

        // 获取日志工厂实例，如果为空则使用NullLoggerFactory
        LoggerFactory = Container.IsRegistered<ILoggerFactory>()
            ? Container.Resolve<ILoggerFactory>()
            : new NullLoggerFactory();
    }
    public DomainHelperBase UserHelper { get; }

    /// <summary>
    /// 配置根
    /// </summary>
    public IConfigurationRoot Configuration { get; }
    /// <summary>
    /// 日志工厂
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }
    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServicesProvider { get; }
    /// <summary>
    /// Autofac容器
    /// </summary>
    public IContainer Container { get; }

    /// <summary>
    /// SessionManager 实例
    /// </summary>
    internal ISessionManager SessionManager => field ??= Container.Resolve<ISessionManager>();

    /// <summary>
    /// 控制器契约的并发字典缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, DomainContracts> _ControllerContracts = new();

    /// <summary>
    /// 创建新的领域上下文
    /// </summary>
    /// <param name="invocation">代理调用信息</param>
    /// <param name="lifetimeScope">生命周期范围</param>
    /// <returns>领域上下文实例</returns>
    internal DomainContext NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
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
            interfaceContract = new DomainContracts();
            var filters = interfaceType.GetCustomAttributes<DomainActionFilterAttribute>(true);
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
            methodContract = new DomainContracts();
            var method = invocation.Method;
            var filters = method.GetCustomAttributes<DomainActionFilterAttribute>(true);
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
        var context = lifetimeScope.Resolve<DomainContext>(
            TypedParameter.From(((DomainServiceBase)invocation.InvocationTarget).User),
            TypedParameter.From(invocation),
            TypedParameter.From(methodContract));
        return context;
    }

    /// <summary>
    /// 获取DomainHost实例的工厂方法
    /// </summary>
    public static Func<DomainHost> Factory => () => Root;

    #region 会话相关方法

    /// <summary>
    /// 新的游客会话
    /// </summary>
    public async Task<SessionInfo> NewGuestSessionAsync()
    {
        var session = await SessionManager.NewSessionAsync();  // 创建新的会话：User == null
        var newSession = await UserHelper.CreateNewGuestSessionAsync(session);      // 调用用户助手：创建用户并绑定到会话
        SessionManager.OnSessionCreated(newSession);   // 会话创建后事件
        return newSession;
    }

    /// <summary>
    /// 获取领域用户并更新会话
    /// </summary>
    internal async Task<DomainUser> GetDomainUserAsync(string sessionKey)
    {
        // Session 存在，获取并激活用户
        var session = await SessionManager.GetAndActiveSessionAsync(sessionKey)
            .ConfigureAwait(false);

        return session.User;
    }

    #endregion
}