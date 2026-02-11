using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

public abstract class DomainHostInitializerBase<TUserInfo>
where TUserInfo : class, IUserInfo, new()
{
    protected bool IsDevelopment { get; private set; }    // 是否处于开发环境
    protected string ConnectionString { get; private set; } = string.Empty; // 数据库连接字符串
    protected Dictionary<string, string> ConfigDictionary { get; } = new(); // 其他配置项
    private DomainHost<TUserInfo>? Host
    {
        get => field ?? throw new InvalidOperationException("Host 尚未初始化，无法访问");
        set;
    }

    /// <summary>
    /// 配置 DMP_Lite 领域服务（数据库、日志、AOP等）
    /// </summary>
    /// <param name="containerBuilder">Autofac 容器构建器</param>
    /// <param name="services">.NET Core DI 服务集合</param>
    /// <param name="configuration">配置项</param>
    /// <returns>配置好的 DMPDomainHelper 实例</returns>
    public DomainUserHelperBase<TUserInfo> InitializeDiContainer(
        ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration)
    {
        // 初始化容器所需的配置项，例如是否处于开发环境、数据库连接字符串等
        OnConfigInitializer(containerBuilder, configuration,
            ConfigDictionary, out var isDevelopment, out var connectionString);

        IsDevelopment = isDevelopment;
        ConnectionString = connectionString;

        // 注册领域基础设施服务
        OnRegisterInfrastructureServices(containerBuilder, services, configuration);
        // 注册领域服务
        return OnRegisterDomainServices(containerBuilder, services, configuration);
    }

    /// <summary>
    /// 配置初始化器：比如根据环境确认是否调试模式、读取连接字符串等。
    /// </summary>
    /// <param name="containerBuilder">容器构建器，用于注册依赖注入的组件和服务</param>
    /// <param name="configuration">应用程序配置设置，用于配置服务和组件</param>
    /// <param name="configDictionary">其他配置项</param>
    /// <param name="isDevelopment">是否处于开发环境</param>
    /// <param name="connectionString">数据库连接字符串</param>
    protected abstract void OnConfigInitializer(ContainerBuilder containerBuilder,
        IConfiguration? configuration, Dictionary<string, string> configDictionary, out bool isDevelopment,
        out string connectionString);

    /// <summary>
    /// 注册领域基础设施服务，例如数据库上下文、日志记录、缓存等。这些服务通常是领域层依赖的第三方组件。
    /// </summary>
    protected virtual void OnRegisterInfrastructureServices(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration)
    {
        // 注册会话管理器
        UseDefaultSessionManager(containerBuilder);
        // 派生类可根据需要覆盖此方法进行额外的配置或初始化
    }

    /// <summary>
    /// 注册领域服务，例如领域服务、领域事件处理器、领域模型等。这些服务通常是领域层的核心组件，直接支持业务逻辑实现。
    /// </summary>
    protected abstract DomainUserHelperBase<TUserInfo> OnRegisterDomainServices(ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    /// <remarks>重写此方法可以在容器完全构建后、但在使用容器解析服务之前执行自定义操作。
    /// 此方法适用于需要构建后配置的高级场景。</remarks>
    protected internal virtual void OnContainerBuilt(IContainer? container, IConfiguration? configuration,
        bool isExternalContainer = false)
    {
        // 默认实现不执行任何操作，派生类可根据需要覆盖此方法进行额外的配置或初始化
    }


    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    internal void ContainerBuiltCallback(DomainHost<TUserInfo> host, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(host);

        // 设置 Host 属性，供后续方法使用
        Host = host;
        host.IsDevelopment = IsDevelopment;

        // 构建基本的全局过滤器（例如权限过滤器、日志过滤器等），不需要注册和注入
        ConfigGlobalFilters(host.Container, host.Configuration);

        // 调用派生类的 OnContainerBuilt 方法，允许进行额外的配置或初始化
        OnContainerBuilt(host.Container, host.Configuration, isExternalContainer);
    }

    /// <summary>
    /// 构建基本的全局过滤器（例如权限过滤器、日志过滤器等），不需要注册和注入
    /// 这些过滤器将应用于所有领域方法调用，提供统一的横切关注点处理。
    /// 表现层的派生类可在此注册全局异常工厂覆盖默认全局异常日志工厂
    /// 例如：Web/Desktop 替换默认全局异常工厂（例如记录日志、设置 HTTP 响应等）
    ///     UseExceptionLoggerFactory{WebGlobalExceptionFactory}();
    /// 注入业务级别的过滤器，例如：base.AddGlobalFilter(new MerchantFilter());
    /// <remarks>注意：后续可通过 builder.ConfigDomainFilters(cfg =>{ cfg.EnableAuthorityFilter()}) 、
    /// app.ConfigDomainWeb(cfg =>{ cfg.UseSessionUserMiddleware()}) 代替</remarks>
    /// </summary>
    protected virtual void ConfigGlobalFilters(IContainer? container, IConfiguration? configuration)
    {
        // 启用、注册全局过滤器（例如权限过滤器、日志过滤器等）

        // 1. 所有环境都开启权限检查（最低安全保障）
        EnableAuthorityFilter();
        // 2. 使用默认全局异常工厂：可在 Web/Desktop 环境替换为适合该环境的全局异常工厂（例如记录日志、设置 HTTP 响应等）
        if (IsDevelopment)
            UseDefaultExceptionLoggerFactory();
        //else UseNullExceptionLoggerFactory();

        // 3. 启用日志：开发环境额外开启详细日志，生产环境甚至不开日志
        if (IsDevelopment)
            EnableDomainLogging(EnumDomainLogLevel.Minimal);
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
    /// 使用默认的会话管理器实现，提供基本的会话管理功能，例如创建、更新、销毁会话等。
    /// </summary>
    protected ContainerBuilder UseDefaultSessionManager(ContainerBuilder containerBuilder)
    {
        containerBuilder.UseSessionManager<SessionManager<TUserInfo>, TUserInfo>();
        return containerBuilder;
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