using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public static class DomainDiExtensions
{
    #region 核心注册策略：Forced (强自治) vs Replaceable (可扩展)

    /// <summary>
    /// 强制注册类型（单例）：利用 PreserveExistingDefaults 确保它是解析的首选，
    /// 即使后续通过 Populate(services) 导入了同接口注册，也会保留此领域实现。
    /// </summary>
    public static IRegistrationBuilder<TService, ConcreteReflectionActivatorData, SingleRegistrationStyle>
        RegisterTypeForced<TService, TImplementer>(this ContainerBuilder builder)
        where TImplementer : class, TService
        where TService : class
    {
        return builder.RegisterType<TImplementer>()
            .As<TService>()
            .PreserveExistingDefaults()
            .SingleInstance();
    }

    /// <summary>
    /// 注册可被覆盖的类型（单例）：遵循 Autofac “最后注册胜出”原则。
    /// 适用于需要允许表现层替换的基础设施（如日志、缓存）。
    /// </summary>
    public static IRegistrationBuilder<TService, ConcreteReflectionActivatorData, SingleRegistrationStyle>
        RegisterTypeReplaceable<TService, TImplementer>(this ContainerBuilder builder)
        where TImplementer : class, TService
        where TService : class
    {
        return builder.RegisterType<TImplementer>()
            .As<TService>()
            .SingleInstance();
    }

    #endregion

    #region 日志基础设施 (Replaceable)

    public static void UseLogger(this ContainerBuilder builder)
    {
        builder.RegisterTypeReplaceable<ILoggerFactory, LoggerFactory>();
    }

    public static void UseLogger(this ContainerBuilder builder, ILoggerFactory loggerFactory)
    {
        loggerFactory.EnsureNotNull(nameof(loggerFactory));
        builder.RegisterInstance(loggerFactory).As<ILoggerFactory>().SingleInstance();
    }

    #endregion

    #region 业务服务注册 (默认 Forced，维持领域自治)

    /// <summary>
    /// 注册受拦截的 Aop 服务（单例且强制，防止被表现层覆盖）
    /// </summary>
    public static IRegistrationBuilder<TContract, ConcreteReflectionActivatorData, SingleRegistrationStyle>
        AddAopService<TContract, TImplementer, TUserInfo>(this ContainerBuilder builder)
        where TContract : class, IAopContract
        where TImplementer : class, TContract
        where TUserInfo : class, IUserInfo, new()
    {
        return builder.RegisterType<TImplementer>()
            .As<TContract>()
            .EnableInterfaceInterceptors()
            .InterceptedBy(typeof(DomainInterceptor<TUserInfo>))
            .PreserveExistingDefaults()
            .SingleInstance();
    }

    /// <summary>
    /// 注册普通领域服务（单例且强制）
    /// </summary>
    public static IRegistrationBuilder<TLimit, ConcreteReflectionActivatorData, SingleRegistrationStyle>
        AddService<TLimit>(this ContainerBuilder builder)
        where TLimit : class, IDomainService
    {
        return builder.RegisterType<TLimit>()
            .AsSelf()
            .AsImplementedInterfaces()
            .PreserveExistingDefaults()
            .SingleInstance();
    }

    /// <summary>
    /// 强制注册指定的单例实例
    /// </summary>
    public static void RegisterInstanceForced<TService>(this ContainerBuilder builder, TService instance)
        where TService : class
    {
        instance.EnsureNotNull(nameof(instance));
        builder.RegisterInstance(instance).As<TService>().PreserveExistingDefaults().SingleInstance();
    }

    #endregion
}