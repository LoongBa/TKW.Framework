using System;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Diagnostics;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public abstract class DomainHostInitializerBase<TUserInfo, TDomainHelper>
where TUserInfo : class, IUserInfo, new()
where TDomainHelper : DomainHelperBase<TUserInfo>
{
    private DomainHost<TUserInfo>? Host
    {
        get => field ?? throw new InvalidOperationException("Host 尚未初始化，无法访问");
        set;
    }

    /// <summary>
    /// 配置依赖注入容器，添加应用程序特定的服务和设置
    /// </summary>
    /// <remarks>重写此方法以注册应用程序所需的自定义服务、模块或配置源。
    /// 此方法通常在应用程序启动期间调用，用于在构建依赖注入容器之前对其进行准备。</remarks>
    /// <param name="containerBuilder">容器构建器，用于注册依赖注入的组件和服务</param>
    /// <param name="services">服务描述符集合，可以向其中添加或配置服务</param>
    /// <param name="configuration">应用程序配置设置，用于配置服务和组件</param>
    protected abstract void OnInitializeContainer(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 注册领域服务，例如领域服务、领域事件处理器、领域模型等。这些服务通常是领域层的核心组件，直接支持业务逻辑实现。
    /// </summary>
    protected abstract TDomainHelper OnRegisterDomainServices(ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 注册领域基础设施服务，例如数据库上下文、日志记录、缓存等。这些服务通常是领域层依赖的第三方组件。
    /// </summary>
    protected abstract void OnRegisterInfrastructureServices(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    /// <remarks>重写此方法可以在容器完全构建后、但在使用容器解析服务之前执行自定义操作。
    /// 此方法适用于需要构建后配置的高级场景。</remarks>
    protected internal abstract void OnContainerBuilt(IContainer? container, IConfiguration? configuration,
        bool isExternalContainer = false);

    /// <summary>
    /// 构造默认的全局过滤器，例如日志记录、权限检查等。
    /// 这些过滤器将应用于所有领域方法调用，提供统一的横切关注点处理。
    /// 例如：base.EnableDomainLogging(); base.EnableAuthorityFilter();
    /// </summary>
    protected abstract void ConfigureGlobalFilterInstances(IContainer? container, IConfiguration? configuration);

    /// <summary>
    /// 配置 DMP_Lite 领域服务（数据库、日志、AOP等）
    /// </summary>
    /// <param name="containerBuilder">Autofac 容器构建器</param>
    /// <param name="services">.NET Core DI 服务集合</param>
    /// <param name="configuration">配置项</param>
    /// <returns>配置好的 DMPDomainHelper 实例</returns>
    public TDomainHelper InitializeDiContainer(
        ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration)
    {
        // 注册默认全局异常工厂（可被应用层替换）
        containerBuilder.RegisterType<DefaultExceptionLoggerFactory>().AsSelf().AsImplementedInterfaces();

        // 初始化容器
        OnInitializeContainer(containerBuilder, services, configuration);
        // 注册领域基础设施服务
        OnRegisterInfrastructureServices(containerBuilder, services, configuration);
        // 注册领域服务
        return OnRegisterDomainServices(containerBuilder, services, configuration);
    }

    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    internal void ContainerBuiltCallback(DomainHost<TUserInfo> host, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(host);

        // 设置 Host 属性，供后续方法使用
        Host = host;
        // 调用虚方法，让派生类决定默认 Filter
        ConfigureGlobalFilterInstances(host.Container, host.Configuration);

        // 调用派生类的 OnContainerBuilt 方法，允许进行额外的配置或初始化
        OnContainerBuilt(host.Container, host.Configuration, isExternalContainer);
    }

    #region 供派生类使用的设置全局过滤器的函数

    /// <summary>
    /// 添加单个全局过滤器（推荐在 OnContainerBuilt 或扩展方法中调用）
    /// </summary>
    protected void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter)
    {
        Host?.AddGlobalFilter(filter);
    }

    /// <summary>
    /// 批量添加全局过滤器
    /// </summary>
    protected void AddGlobalFilters(IEnumerable<DomainFilterAttribute<TUserInfo>> filters)
    {
        Host?.AddGlobalFilters(filters);
    }
    /// <summary>
    /// 允许启用领域日志记录过滤器，自动记录领域方法的调用、参数、返回值和异常等信息。
    /// 日志级别可配置（Normal、Detailed等）。需要配合具体的日志实现（例如 Microsoft.Extensions.Logging）使用。
    /// <see cref="LoggingFilterAttribute{TUserInfo}"/>
    /// </summary>
    protected void EnableDomainLogging(EnumDomainLogLevel level = EnumDomainLogLevel.Normal)
    {
        Host?.AddGlobalFilter(new LoggingFilterAttribute<TUserInfo>(level));
    }

    /// <summary>
    /// 允许启用权限过滤器，自动检查用户权限并拒绝未授权访问。
    /// 需要配合具体的权限实现（例如基于角色、基于声明等）使用。
    /// <see cref="AuthorityFilterAttribute{TUserInfo}"/>
    /// </summary>
    protected void EnableAuthorityFilter()
    {
        Host?.AddGlobalFilter(new AuthorityFilterAttribute<TUserInfo>());
    }

    /// <summary>
    /// 使用默认的全局异常工厂来处理领域方法调用过程中未捕获的异常。
    /// 全局异常工厂允许集中处理异常，例如记录日志、转换异常类型、返回统一的错误响应等。
    /// </summary>
    protected void UseDefaultExceptionLoggerFactory(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
    {
        UseExceptionLoggerFactory<DefaultExceptionLoggerFactory>(logLevel);
    }

    /// <summary>
    /// 使用自定义的全局异常工厂来处理领域方法调用过程中未捕获的异常。
    /// 全局异常工厂允许集中处理异常，例如记录日志、转换异常类型、返回统一的错误响应等。
    /// </summary>
    protected void UseExceptionLoggerFactory(DefaultExceptionLoggerFactory exceptionLoggerFactory)
    {
        Host?.ExceptionLoggerFactory = exceptionLoggerFactory;
    }

    /// <summary>
    /// 使用自定义的全局异常工厂来处理领域方法调用过程中未捕获的异常。
    /// 全局异常工厂允许集中处理异常，例如记录日志、转换异常类型、返回统一的错误响应等。
    /// </summary>
    protected void UseExceptionLoggerFactory<TExceptionLoggerFactory>(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
    where TExceptionLoggerFactory : DefaultExceptionLoggerFactory, new()
    {
        Host?.ExceptionLoggerFactory = new TExceptionLoggerFactory()
            .SetLoggerFactory(Host.LoggerFactory).SetLogLevel(logLevel);
    }

    #endregion
}