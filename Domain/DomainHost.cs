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
public sealed class DomainHost<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public static DomainHost<TUserInfo>? Root { get; private set; }
    public bool IsExternalContainer { get; private set; }
    public bool IsDevelopment { get; internal set; }

    private readonly List<DomainFilterAttribute<TUserInfo>> _GlobalFilters = [];
    public IReadOnlyList<DomainFilterAttribute<TUserInfo>> GlobalFilters => _GlobalFilters.AsReadOnly();

    private readonly ConcurrentDictionary<string, DomainContracts<TUserInfo>> _ControllerContracts = new();

    internal void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (_GlobalFilters.Contains(filter)) return;
        if (_GlobalFilters.Any(f => f.GetType() == filter.GetType())) return;
        _GlobalFilters.Add(filter);
    }

    internal void AddGlobalFilters(IEnumerable<DomainFilterAttribute<TUserInfo>> filters) => _GlobalFilters.AddRange(filters);

    /// <summary>
    /// 初始化 DomainHost 的静态入口方法
    /// </summary>
    public static DomainHost<TUserInfo> Build<TDomainInitializer>(
        ContainerBuilder? upLevelContainer = null,
        IConfiguration? configuration = null,
        DomainOptions? options = null) // 重构点：接收表现层配置
        where TDomainInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        if (Root != null)
            throw new InvalidOperationException("不能重复初始化 DomainHost");

        var isExternalContainer = upLevelContainer != null;
        var containerBuilder = upLevelContainer ?? new ContainerBuilder();

        containerBuilder.RegisterType<DomainContext<TUserInfo>>();
        containerBuilder.RegisterType<DomainInterceptor<TUserInfo>>();

        // 确保 options 不为 null
        options ??= new DomainOptions();

        IServiceCollection services = new ServiceCollection();
        var initializer = new TDomainInitializer();

        // 调用 Initializer，此时 options 包含表现层的初始值，领域层可在内部进行“守门员”修正
        var userHelper = initializer.InitializeDiContainer(containerBuilder, services, configuration, options);

        Root = new DomainHost<TUserInfo>(userHelper, containerBuilder, services, configuration, isExternalContainer);
        containerBuilder.RegisterInstance(Root).AsSelf();
        containerBuilder.Register(_ => Root); // 保留原始代码的双重注册

        containerBuilder.RegisterBuildCallback(context =>
        {
            if (Root == null) return;

            if (context is IContainer container)
                Root.Container = container;
            else
            {
                // 保留原始代码中的健壮性处理逻辑
                try
                {
                    var maybe = context.ResolveOptional<IContainer>();
                    if (maybe != null) Root.Container = maybe;
                }
                catch { /* 忽略异常以保持启动 */ }
            }

            if (Root.Container != null)
            {
                initializer.ContainerBuiltCallback(Root, isExternalContainer);

                if (Root.Container.IsRegistered<ILoggerFactory>())
                    Root.LoggerFactory = Root.Container.Resolve<ILoggerFactory>();
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

        containerBuilder.EnsureNotNull(nameof(containerBuilder));
        containerBuilder.Populate(services); // 将 Web 层的注册 Populate 进 Autofac

        Configuration = configuration;
    }

    public DomainUserHelperBase<TUserInfo> UserHelper { get; }
    public IConfiguration? Configuration { get; private set; }
    public ILoggerFactory LoggerFactory { get; private set; } = new NullLoggerFactory();
    public DefaultExceptionLoggerFactory? ExceptionLoggerFactory { get; internal set; }
    public IContainer? Container { get; private set; }
    internal ISessionManager<TUserInfo>? SessionManager => field ??= Container?.Resolve<ISessionManager<TUserInfo>>();

    /// <summary>
    /// 创建新的领域上下文（供 DomainInterceptor 调用）
    /// </summary>
    /// <summary>
    /// 创建新的领域上下文（供 DomainInterceptor 调用）
    /// </summary>
    internal DomainContext<TUserInfo> NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
    {
        // 1. 查找核心业务接口（排除 IDomainService 等基础框架接口）
        var interfaceType = invocation.TargetType.GetInterfaces()
            .First(i => !i.Name.StartsWith("IDomainService", StringComparison.OrdinalIgnoreCase));

        // 2. 处理并缓存控制器（接口）级的过滤器与标志
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

        // 3. 处理并缓存方法级的过滤器与标志
        key = $"{invocation.TargetType.FullName}:{invocation.Method.Name}";
        if (!_ControllerContracts.TryGetValue(key, out var methodContract))
        {
            methodContract = new DomainContracts<TUserInfo>();
            var method = invocation.Method;
            var filters = method.GetCustomAttributes<DomainFilterAttribute<TUserInfo>>(true);
            var flags = method.GetCustomAttributes<DomainFlagAttribute>(true);

            // 合并方法级与控制器级的配置
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

        // 4. 核心联动：将当前 AOP 开启的子作用域存入异步上下文（AsyncLocal）
        // 这确保了在该方法随后的异步流中，User.Use<T> 会自动使用此 lifetimeScope
        DomainUser<TUserInfo>._ActiveScope.Value = lifetimeScope;

        // 5. 手动实例化 DomainContext，跳过 Autofac Resolve 以提升性能
        return new DomainContext<TUserInfo>(
            ((DomainServiceBase<TUserInfo>)invocation.InvocationTarget).User,
            invocation,
            methodContract,
            lifetimeScope,
            LoggerFactory);
    }

    public static Func<DomainHost<TUserInfo>> Factory => () => Root!;

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
}