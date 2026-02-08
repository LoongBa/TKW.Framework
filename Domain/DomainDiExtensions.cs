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
    #region 全局日志（基础设施，可被表现层覆盖，不强制单例）

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
            loggerFactory.EnsureNotNull(nameof(loggerFactory));
            return left.RegisterInstance(loggerFactory).As<ILoggerFactory>();
        }
    }

    #endregion

    #region 会话二级缓存（强制单例，核心基础设施）

    public static IRegistrationBuilder<ISessionManager<TUserInfo>, ConcreteReflectionActivatorData, SingleRegistrationStyle>
        UseSessionManager<TSessionManager, TUserInfo>(this ContainerBuilder left)
        where TSessionManager : class, ISessionManager<TUserInfo>
        where TUserInfo : class, IUserInfo, new()
    {
        return left.RegisterType<TSessionManager>()
            .As<ISessionManager<TUserInfo>>()
            .AsSelf()
            .SingleInstance(); // 强制单例，不允许覆盖
    }

    #endregion

    #region 数据库相关配置（强制单例）

    public static IRegistrationBuilder<IDomainDataAccessHelper, SimpleActivatorData, SingleRegistrationStyle>
        AddDomainDataAccessHelper<TIDomainDbContextFactory>(this ContainerBuilder left, TIDomainDbContextFactory domainDbContextFactory)
        where TIDomainDbContextFactory : class, IDomainDataAccessHelper
    {
        domainDbContextFactory.EnsureNotNull(nameof(domainDbContextFactory));
        return left.RegisterInstance(domainDbContextFactory)
            .SingleInstance()
            .As<TIDomainDbContextFactory>();
    }

    #endregion

    #region 控制器与服务注册（核心领域逻辑，默认强制单例，不允许表现层覆盖）

    extension(ContainerBuilder builder)
    {
        /// <summary>
        /// 注册普通领域服务（不受拦截器影响，单例且强制，不允许表现层覆盖）
        /// </summary>
        public IRegistrationBuilder<TLimit, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddService<TLimit>()
            where TLimit : class, IDomainService
        {
            return builder.RegisterType<TLimit>().SingleInstance();
        }

        /// <summary>
        /// 批量注册普通领域服务（单例且强制）
        /// </summary>
        public IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
            AddServices<TLimit>(params Type[] implementations)
            where TLimit : class, IDomainService
        {
            var validTypes = implementations
                .Where(t => typeof(TLimit).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToArray();

            return builder.RegisterTypes(validTypes).SingleInstance();
        }

        /// <summary>
        /// 注册受拦截的 Aop 服务（单例且强制，不允许表现层覆盖）
        /// </summary>
        public IRegistrationBuilder<TContract, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddAopService<TContract, TImplementer, TUserInfo>()
            where TContract : class, IAopContract
            where TImplementer : class, TContract
            where TUserInfo : class, IUserInfo, new()
        {
            return builder.RegisterType<TImplementer>()
                .As<TContract>()
                .EnableInterfaceInterceptors()
                .InterceptedBy(typeof(DomainInterceptor<TUserInfo>))
                .SingleInstance();
        }

        /// <summary>
        /// 批量注册受拦截的 Aop 服务（单例且强制，不允许表现层覆盖）
        /// </summary>
        public void AddAopServices<TContract, TUserInfo>(params Type[] implementations)
            where TContract : class, IAopContract
            where TUserInfo : class, IUserInfo, new()
        {
            var interceptorType = typeof(DomainInterceptor<>).MakeGenericType(typeof(TUserInfo));

            foreach (var implType in implementations)
            {
                if (!typeof(TContract).IsAssignableFrom(implType) || implType.IsAbstract || implType.IsInterface)
                    continue;

                builder.RegisterType(implType)
                    .As<TContract>()
                    .EnableInterfaceInterceptors()
                    .InterceptedBy(interceptorType)
                    .SingleInstance();
            }
        }

        /// <summary>
        /// 强制注册实例（单例，不允许后续覆盖）
        /// 适用于 FreeSql、配置对象、自定义单例实例等场景
        /// </summary>
        public IRegistrationBuilder<TService, SimpleActivatorData, SingleRegistrationStyle>
            RegisterInstanceForced<TService>(TService instance)
            where TService : class
        {
            instance.EnsureNotNull(nameof(instance));
            return builder.RegisterInstance(instance)
                .As<TService>()
                .SingleInstance();
        }

        /// <summary>
        /// 强制注册类型（单例，不允许后续覆盖）
        /// 适用于需要 Autofac 自动创建实例，但必须强制单例的场景
        /// </summary>
        public IRegistrationBuilder<TService, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            RegisterTypeForced<TService, TImplementer>()
            where TImplementer : class, TService
            where TService : class
        {
            return builder.RegisterType<TImplementer>()
                .As<TService>()
                .SingleInstance();
        }

        /// <summary>
        /// 强制注册类型（单例，自动推导所有接口，不允许后续覆盖）
        /// </summary>
        public IRegistrationBuilder<TImplementer, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            RegisterTypeForced<TImplementer>()
            where TImplementer : class
        {
            return builder.RegisterType<TImplementer>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }

    #endregion
}