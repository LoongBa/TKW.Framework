using System;
using System.Linq;
using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain
{
    public static class DomainDiExtensions
    {
        #region 全局日志

        public static IRegistrationBuilder<ILoggerFactory, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            UseLogger(this ContainerBuilder left)
        {
            return left.RegisterType<ILoggerFactory>().SingleInstance();
        }

        public static IRegistrationBuilder<ILoggerFactory, SimpleActivatorData, SingleRegistrationStyle>
            UseLogger(this ContainerBuilder left, ILoggerFactory loggerFactory)
        {
            loggerFactory.AssertNotNull(name: nameof(loggerFactory));
            return left.RegisterInstance(loggerFactory).As<ILoggerFactory>();
        }

        #endregion

        #region 会话二级缓存

        public static IRegistrationBuilder<ISessionCache, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            UseSessionManager(this ContainerBuilder left)
        {
            return left.RegisterType<ISessionCache>().SingleInstance().As<ISessionCache>();
        }

        public static IRegistrationBuilder<ISessionCache, SimpleActivatorData, SingleRegistrationStyle>
            UseSessionManager<TDomainUser>(this ContainerBuilder left)
        where TDomainUser : DomainUser
        {
            var sessionManager = new SessionManager<TDomainUser>();
            return left.RegisterInstance(sessionManager).As<ISessionCache>();
        }

        public static IRegistrationBuilder<ISessionCache, SimpleActivatorData, SingleRegistrationStyle>
            UseSessionManager(this ContainerBuilder left, ISessionCache sessionCache)
        {
            sessionCache.AssertNotNull(name: nameof(sessionCache));
            return left.RegisterInstance(sessionCache).As<ISessionCache>();
        }

        #endregion
        public static IServiceCollection
            AddDomainSessionHelper<TSessionHelper, TUser>(this IServiceCollection services, TSessionHelper sessionHelper)
            where TSessionHelper : DomainHelperBase<TUser>
            where TUser : DomainUser, ICopyValues<TUser>, new()
        {
            return services.AddSingleton(sessionHelper);
        }

        #region 数据库相关配置

        public static IRegistrationBuilder<IDomainDataAccessHelper, SimpleActivatorData, SingleRegistrationStyle>
            AddDomainDataAccessHelper<TIDomainDbContextFactory>(this ContainerBuilder left, TIDomainDbContextFactory domainDbContextFactory)
        where TIDomainDbContextFactory : class, IDomainDataAccessHelper
        {
            domainDbContextFactory.AssertNotNull(nameof(domainDbContextFactory));
            return left.RegisterInstance(domainDbContextFactory).SingleInstance().As<TIDomainDbContextFactory>();
        }

        #endregion

        #region 控制器

        /// <summary>
        /// 注册服务（不受拦截器影响）
        /// </summary>
        public static IRegistrationBuilder<TDomainService, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddService<TDomainService>(this ContainerBuilder left)
            where TDomainService : class, IDomainService
        {
            return left.RegisterType<TDomainService>();
        }

        /// <summary>
        /// 注册多个服务（不受拦截器影响）
        /// </summary>
        public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
            AddDomainServices<TDomainService>(this ContainerBuilder left, Type[] controllers)
            where TDomainService : class, IDomainService
        {
            var list = controllers.Where(c => c.IsAssignableFrom(typeof(TDomainService))).ToArray();
            return left.RegisterTypes(list);
        }

        /// <summary>
        /// 注册受拦截的控制器
        /// </summary>
        /// <typeparam name="TContractInterface"></typeparam>
        /// <typeparam name="TAopContract"></typeparam>
        /// <param name="left"></param>
        public static IRegistrationBuilder<TAopContract, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddServiceWithAop<TContractInterface, TAopContract>(this ContainerBuilder left)
            where TAopContract : class, Domain.IAopContract
        {
            return left.RegisterType<TAopContract>()
                       .As<TContractInterface>()
                       .EnableInterfaceInterceptors()
                       .InterceptedBy(typeof(DomainInterceptor)); //TKW Domain 领域框架拦截器
        }
        #endregion
    }
}