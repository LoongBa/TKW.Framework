using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain
{
    public sealed class DomainHost
    {
        public static DomainHost Root { get; private set; }

        public static DomainHost Initial<TUser, TUserHelper>(
            Func<ContainerBuilder, IServiceCollection, ConfigurationBuilder,
            IServiceCollection, SessionHelperBase<TUser>> configureServices,
            IServiceCollection upLevelServices)
            where TUser : DomainUser, ICopyValues<TUser>
            where TUserHelper : SessionHelperBase<TUser>
        {
            if (Root != null) throw new InvalidOperationException("不能重复初始化");

            configureServices.AssertNotNull(name: nameof(configureServices));
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory); //默认已设置 BasePath，可覆盖

            IServiceCollection services = new ServiceCollection();
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<DomainContext>();
            containerBuilder.RegisterType<DomainInterceptor>(); //注册领域框架拦截器
            containerBuilder.Register(_ => Root);

            //执行委托的方法
            var userHelper = configureServices(containerBuilder, services, configurationBuilder, upLevelServices).AssertNotNull(nameof(configureServices));
            containerBuilder.RegisterInstance(userHelper).As<TUserHelper>();

            //构造 DomainHost 实例并注入
            Root = new DomainHost(containerBuilder, services, configurationBuilder);
            return Root;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        private DomainHost(ContainerBuilder containerBuilder,
            IServiceCollection services = null, IConfigurationBuilder configurationBuilder = null)
        {
            containerBuilder.AssertNotNull(name: nameof(containerBuilder));

            services ??= new ServiceCollection();
            containerBuilder.Populate(services);

            var configBuilder = configurationBuilder ?? new ConfigurationBuilder();
            containerBuilder.RegisterInstance(Configuration = configBuilder.Build());

            Container = containerBuilder.Build();
            ServicesProvider = new AutofacServiceProvider(Container);

            LoggerFactory = Container.Resolve<ILoggerFactory>() ?? new NullLoggerFactory();
        }

        public IConfigurationRoot Configuration { get; }
        public ILoggerFactory LoggerFactory { get; }
        public IServiceProvider ServicesProvider { get; }
        public IContainer Container { get; }

        public SessionHelperBase<TUser> UserHelper<TUser, TUserHelper>()
            where TUser : DomainUser, ICopyValues<TUser>
            where TUserHelper : SessionHelperBase<TUser>
        {
            return Container.Resolve<TUserHelper>();
        }

        private readonly ConcurrentDictionary<string, DomainContracts> _ControllerContracts = new ConcurrentDictionary<string, DomainContracts>();
        internal DomainContext NewDomainContext(IInvocation invocation, ILifetimeScope lifetimeScope)
        {
            //Notice: 仅处理接口上定义的 Attribute，忽略类的 Attribute
            DomainContracts methodContract;
            DomainContracts interfaceContract;

            //TODO: 处理全局 Filters

            //处理控制器级 Filters
            //var controllerType = invocation.TargetType.GetInterfaces().First(i => i.ImplementedInterfaces.Any(bi=>bi is IDomainServiceContract));
            var interfaceType = invocation.TargetType.GetInterfaces().First(i => !i.Name.StartsWith("IDomainService", StringComparison.OrdinalIgnoreCase));

            var key = $"{interfaceType.FullName}";
            if (_ControllerContracts.TryGetValue(key, out interfaceContract))
            {
            }
            else
            {
                interfaceContract = new DomainContracts();
                var filters = interfaceType.GetCustomAttributes<DomainActionFilterAttribute>(true);
                var flags = interfaceType.GetCustomAttributes<DomainFlagAttribute>(true);

                //加入到缓存中，减少每次重新查询、获取的负载
                foreach (var filter in filters) interfaceContract.ControllerFilters.Add(filter);
                foreach (var flag in flags) interfaceContract.ControllerFlags.Add(flag);
                _ControllerContracts.TryAdd(key, interfaceContract);
            }

            //处理方法级 Filters
            key = $"{invocation.TargetType.FullName}:{invocation.Method.Name}";
            if (_ControllerContracts.TryGetValue(key, out methodContract))
            {
            }
            else
            {
                methodContract = new DomainContracts();
                var method = invocation.Method /*?? invocation.MethodInvocationTarget*/;
                var filters = method.GetCustomAttributes<DomainActionFilterAttribute>(true);
                var flags = method.GetCustomAttributes<DomainFlagAttribute>(true);

                //方法级的 Filters
                foreach (var filter in filters)
                    methodContract.MethodFilters.Add(filter);
                //控制器级的 Filters
                foreach (var filter in interfaceContract.ControllerFilters)
                    methodContract.ControllerFilters.Add(filter);

                //方法级的 Flags
                foreach (var flag in flags)
                    methodContract.MethodFlags.Add(flag);
                //控制器级的 Flags
                foreach (var flag in interfaceContract.ControllerFlags)
                    methodContract.ControllerFlags.Add(flag);

                //加入缓存中，减少每次重新查询、获取的负载
                _ControllerContracts.TryAdd(key, methodContract);
            }
            var context = lifetimeScope.Resolve<DomainContext>(
                TypedParameter.From(((DomainService)invocation.InvocationTarget).DomainUser),
                TypedParameter.From(invocation),
                TypedParameter.From(methodContract));
            return context;
        }
    }
}