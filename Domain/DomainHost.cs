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
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo, TDomainHelper>, new()
        where TDomainHelper : DomainHelperBase<TUserInfo>
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        // 确保不能重复初始化
        if (Root != null) throw new InvalidOperationException("不能重复初始化 DomainHost");

        var isExternalContainer = upLevelContainer != null;
        var containerBuilder = upLevelContainer ?? new ContainerBuilder();

        // 注册领域框架上下文和拦截器
        containerBuilder.RegisterType<DomainContext<TUserInfo>>();
        containerBuilder.RegisterType<DomainInterceptor<TUserInfo>>(); // 注册领域框架拦截器

        // 注册默认全局异常工厂：Web/Desktop 可以替换为具体实现（例如记录日志、设置 HTTP 响应等）
        // 可以被派生类覆盖以提供特定的异常处理逻辑，如 Web 返回适当的 HTTP 响应，而桌面应用程序可能会显示错误消息框。
        containerBuilder.RegisterType<DefaultDomainGlobalExceptionFactory>().AsSelf();
        // 注册默认的空日志类厂，可以被派生类覆盖以提供特定的日志记录实现，例如使用 Serilog、NLog 或其他日志库。
        containerBuilder.UseLogger(new NullLoggerFactory()).As<ILoggerFactory>();

        // 执行委托方法，获取用户助手实例
        IServiceCollection services = new ServiceCollection();
        var initializer = new TDomainInitializer();
        var userHelper = initializer.InitializeDiContainer(containerBuilder, services, configuration);

        // 注册 TSessionManager 实例，需注册依赖的类型或实例，如 HybridCache
        containerBuilder.UseSessionManager<TSessionManager, TUserInfo>();

        // 构造 DomainHost 实例，通过全局唯一的单例获取效率更高
        Root = new DomainHost<TUserInfo>(userHelper, containerBuilder, services, configuration, isExternalContainer);
        containerBuilder.Register(_ => Root);   // 将类厂注入容器
        // 如果最终由外部宿主构建容器，使用 RegisterBuildCallback 捕获构建完成时的容器实例
        if (isExternalContainer)
            containerBuilder.RegisterBuildCallback(context =>
            {
                if (Root == null) return;

                // componentContext 通常 可以 转为 IContainer
                if (context is IContainer container)
                    Root.Container = container;
                else // 兜底：尝试解析 IContainer
                    try
                    {
                        var maybe = context.ResolveOptional<IContainer>();
                        if (maybe != null)
                            Root.Container = maybe;
                    }
                    catch
                    {
                        // 忽略解析异常，保持不抛，以便容器可继续正常启动
                    }

                if (Root.Container != null)
                {
                    // 触发领域主机初始化完成后的回调，允许进行额外的配置或初始化
                    initializer.ContainerBuiltCallback(Root, Root.Container, isExternalContainer);
                    // 获取日志工厂实例，覆盖默认的 NullLoggerFactory
                    if(Root.Container.IsRegistered<ILoggerFactory>())
                        Root.LoggerFactory = Root.Container.Resolve<ILoggerFactory>();
                }
            });
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

    /// <summary>
    /// 配置根
    /// </summary>
    public IConfiguration? Configuration { get; private set; }

    /// <summary>
    /// 日志工厂
    /// </summary>
    public ILoggerFactory LoggerFactory { get; private set; } = new NullLoggerFactory();

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

    /// <summary>
    /// 是否外部容器
    /// </summary>
    public bool IsIsExternalContainer { get; private set; }

    /// <summary>
    /// 创建新的领域上下文
    /// </summary>
    /// <param name="invocation">代理调用信息</param>
    /// <param name="lifetimeScope">生命周期范围</param>
    /// <returns>领域上下文实例</returns>
    internal DomainContext<TUserInfo> NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
    {
        // 处理控制器级和方法级的 Filters + Flags（原有逻辑不变）
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

        // 关键修改：注入 LoggerFactory
        var context = lifetimeScope.Resolve<DomainContext<TUserInfo>>(
            TypedParameter.From(((DomainServiceBase<TUserInfo>)invocation.InvocationTarget).User),
            TypedParameter.From(invocation),
            TypedParameter.From(methodContract),
            TypedParameter.From(LoggerFactory));   // ← 这里注入

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