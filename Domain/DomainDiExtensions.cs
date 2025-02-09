using System;
using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Scanning;
using Microsoft.EntityFrameworkCore;
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
        where TDomainUser : DomainUser, ICopyValues<TDomainUser>
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
            where TSessionHelper : SessionHelperBase<TUser>
            where TUser : DomainUser, ICopyValues<TUser>
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
        #region 多个业务 Manager

        public static IRegistrationBuilder<TDomainManager, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddDomainManager<TIDomainDbContextFactory, TDomainManager>(this ContainerBuilder left)
            where TDomainManager : AbstractDomainManager<TIDomainDbContextFactory>
            where TIDomainDbContextFactory : IDomainDataAccessHelper
        {
            return left.RegisterType<TDomainManager>().SingleInstance();
        }

        public static IRegistrationBuilder<AbstractDomainManager<TIDomainDbContextFactory>, SimpleActivatorData, SingleRegistrationStyle>
            AddDomainManager<TIDomainDbContextFactory>(this ContainerBuilder left, AbstractDomainManager<TIDomainDbContextFactory> domainManager)
            where TIDomainDbContextFactory : IDomainDataAccessHelper
        {
            domainManager.AssertNotNull(name: nameof(domainManager));
            return left.RegisterInstance(domainManager);
        }
        #endregion

        #region 控制器

        /// <summary>
        /// 注册控制器（不受拦截器影响）
        /// </summary>
        public static IRegistrationBuilder<TDomainController, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddDomainService<TDomainController>(this ContainerBuilder left)
            where TDomainController : class, IDomainService
        {
            return left.RegisterType<TDomainController>();
        }

        /// <summary>
        /// 注册多个控制器（不受拦截器影响）
        /// </summary>
        public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
            AddControllers(this ContainerBuilder left, Type[] controllers)
        {
            return left.RegisterTypes(controllers);
        }

        /// <summary>
        /// 注册受拦截的控制器
        /// </summary>
        /// <typeparam name="TContractInterface"></typeparam>
        /// <typeparam name="TDomainController"></typeparam>
        /// <param name="left"></param>
        public static IRegistrationBuilder<TDomainController, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddDomainServiceIntercepted<TContractInterface, TDomainController>(this ContainerBuilder left)
            where TDomainController : class, IDomainService
        {
            return left.RegisterType<TDomainController>()
                       .As<TContractInterface>()
                       .EnableInterfaceInterceptors()
                       .InterceptedBy(typeof(DomainInterceptor)); //TKW Domain 领域框架拦截器
        }
        #endregion
    }
}