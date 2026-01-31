using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Scanning;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

public static class DomainDiExtensions
{
    #region 全局日志

    extension(ContainerBuilder left)
    {
        public IRegistrationBuilder<ILoggerFactory, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            UseLogger()
        {
            return left.RegisterType<ILoggerFactory>().SingleInstance();
        }

        public IRegistrationBuilder<ILoggerFactory, SimpleActivatorData, SingleRegistrationStyle>
            UseLogger(ILoggerFactory loggerFactory)
        {
            loggerFactory.EnsureNotNull(name: nameof(loggerFactory));
            return left.RegisterInstance(loggerFactory).As<ILoggerFactory>();
        }
    }

    #endregion

    #region 会话二级缓存

    extension(ContainerBuilder left)
    {
        public IRegistrationBuilder<ISessionManager, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            UseSessionManager()
        {
            return left.RegisterType<SessionManager>().As<ISessionManager>()
                .SingleInstance();
        }
    }

    #endregion

    #region 数据库相关配置

    public static IRegistrationBuilder<IDomainDataAccessHelper, SimpleActivatorData, SingleRegistrationStyle>
        AddDomainDataAccessHelper<TIDomainDbContextFactory>(this ContainerBuilder left, TIDomainDbContextFactory domainDbContextFactory)
        where TIDomainDbContextFactory : class, IDomainDataAccessHelper
    {
        domainDbContextFactory.EnsureNotNull(nameof(domainDbContextFactory));
        return left.RegisterInstance(domainDbContextFactory).SingleInstance().As<TIDomainDbContextFactory>();
    }

    #endregion

    #region 控制器

    /// <param name="left"></param>
    extension(ContainerBuilder left)
    {
        /// <summary>
        /// 注册服务（不受拦截器影响）
        /// </summary>
        public IRegistrationBuilder<TDomainService, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddService<TDomainService>()
            where TDomainService : class, IDomainService
        {
            return left.RegisterType<TDomainService>();
        }

        /// <summary>
        /// 注册多个服务（不受拦截器影响）
        /// </summary>
        public IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
            AddDomainServices<TDomainService>(Type[] controllers)
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
        public IRegistrationBuilder<TAopContract, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddAopService<TContractInterface, TAopContract>()
            where TAopContract : class, IAopContract
        {
            return left.RegisterType<TAopContract>()
                .As<TContractInterface>()
                .EnableInterfaceInterceptors()
                .InterceptedBy(typeof(DomainInterceptor)); //TKW Domain 领域框架拦截器
        }
    }

    #endregion
}