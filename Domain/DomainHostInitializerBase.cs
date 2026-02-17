using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机初始化基类
/// 负责领域层服务的注册、基础设施配置以及 DI 容器构建后的回调逻辑。
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public abstract class DomainHostInitializerBase<TUserInfo>
where TUserInfo : class, IUserInfo, new()
{
    // 基础属性，用于在领域层内快捷访问表现层传递的参数
    protected bool IsDevelopment { get; private set; }
    protected string ConnectionString { get; private set; } = string.Empty;
    protected Dictionary<string, string> ConfigDictionary { get; private set; } = new();

    /// <summary>
    /// 当前初始化器关联的领域主机实例
    /// </summary>
    protected DomainHost<TUserInfo>? Host { get; private set; }

    /// <summary>
    /// 初始化领域服务容器（由 DomainHost.Build 调用）
    /// </summary>
    public DomainUserHelperBase<TUserInfo> InitializeDiContainer(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        // 1. 第一次同步：获取来自表现层的初始设定
        SyncOptionsToProperties(options);

        // 2. 领域层“守门员”逻辑：允许具体领域项目在注册前根据业务安全要求强制修正参数
        OnPreInitialize(options, configuration);

        // 3. 第二次同步：确保领域层在 OnPreInitialize 中所做的修改能反映到初始化器属性中
        SyncOptionsToProperties(options);

        // 4. 注册基础设施服务（包含日志能力、会话管理等）
        RegisterInfrastructureInternal(containerBuilder, services, configuration, options);

        // 5. 注册业务逻辑服务（由子类实现）
        return OnRegisterDomainServices(containerBuilder, services, configuration);
    }

    private void SyncOptionsToProperties(DomainOptions options)
    {
        IsDevelopment = options.IsDevelopment;
        ConnectionString = options.ConnectionString;
        ConfigDictionary = options.ConfigDictionary;
    }

    /// <summary>
    /// 预初始化钩子：
    /// 领域层子类可在此检查 options 参数，例如强制要求生产环境必须使用某种配置。
    /// </summary>
    protected virtual void OnPreInitialize(DomainOptions options, IConfiguration? configuration) { }

    /// <summary>
    /// 注册领域层基础设施服务
    /// </summary>
    protected virtual void RegisterInfrastructureInternal(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        // 1. 执行派生类的“钩子”方法，让业务层先进行配置（如 JSON 序列化设置）
        OnRegisterInfrastructureServices(containerBuilder, services, configuration, options);

        // 2. 强制执行框架基类的基础设施注册
        // 配置 HybridCache：SessionManager 默认使用内存缓存
        services.AddHybridCache();

        // 内存版 SessionManager 必须注册为单例 (SingleInstance)
        // 否则在 Scoped 生命周期下，跨请求的会话数据将无法共享，导致“孤儿 Cookie”问题无法闭环
        containerBuilder.RegisterTypeReplaceable<ISessionManager<TUserInfo>, SessionManager<TUserInfo>>()
            .SingleInstance();

        // 开启 AOP 日志注入能力
        if (options.EnableDomainLogging)
            containerBuilder.UseLogger();
    }

    /// <summary>
    /// 注册领域层基础设施服务
    /// </summary>
    protected abstract void OnRegisterInfrastructureServices(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration, DomainOptions options);

    /// <summary>
    /// 注册领域业务服务（必须由具体的领域项目实现）
    /// </summary>
    protected abstract DomainUserHelperBase<TUserInfo> OnRegisterDomainServices(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration);

    /// <summary>
    /// 容器构建完成后的通知（子类可重写）
    /// </summary>
    protected internal virtual void OnContainerBuilt(IContainer? container, IConfiguration? configuration, bool isExternalContainer = false) { }

    /// <summary>
    /// 内部回调：处理容器构建后的核心对象绑定
    /// </summary>
    internal void ContainerBuiltCallback(DomainHost<TUserInfo> host, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        host.IsDevelopment = IsDevelopment;

        // 提取局部变量并进行空检查，彻底消除 CS8604 警告
        var container = host.Container;
        if (container != null)
        {
            // 【日志防重复逻辑】：
            // 1. 如果 Host 已经持有了有效的 LoggerFactory（来自 Web DI 的 Populate），则不再覆盖。
            // 2. 如果还是 NullLoggerFactory，则尝试从容器解析。
            if (host.LoggerFactory is Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory
                && container.IsRegistered<ILoggerFactory>())
            {
                host.LoggerFactory = container.Resolve<ILoggerFactory>();
            }

            // 配置全局 AOP 过滤器（此时确保传入非空容器）
            ConfigGlobalFilters(container, host.Configuration);
        }

        // 通知业务子类（保留原有的可空传递，由子类自行判断）
        OnContainerBuilt(host.Container, host.Configuration, isExternalContainer);
    }

    /// <summary>
    /// 配置全局 AOP 拦截过滤器
    /// </summary>
    protected virtual void ConfigGlobalFilters(IContainer? container, IConfiguration? configuration)
    {
        // 默认启用权限检查过滤器
        EnableAuthorityFilter();

        // 开发环境下默认启用控制台详细异常记录
        if (IsDevelopment)
            UseDefaultExceptionLoggerFactory();
    }

    #region 过滤器与辅助控制方法

    /// <summary>
    /// 向领域主机添加全局过滤器
    /// </summary>
    protected void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter) => Host?.AddGlobalFilter(filter);

    /// <summary>
    /// 显式开启领域层 AOP 日志记录
    /// </summary>
    protected void EnableDomainLogging(EnumDomainLogLevel level = EnumDomainLogLevel.Normal)
        => Host?.AddGlobalFilter(new LoggingFilterAttribute<TUserInfo>(level));

    /// <summary>
    /// 显式开启领域层权限验证
    /// </summary>
    protected void EnableAuthorityFilter()
        => Host?.AddGlobalFilter(new AuthorityFilterAttribute<TUserInfo>());

    /// <summary>
    /// 使用框架默认的异常日志记录工厂
    /// </summary>
    protected void UseDefaultExceptionLoggerFactory(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
        => UseExceptionLoggerFactory<DefaultExceptionLoggerFactory>(logLevel);

    /// <summary>
    /// 使用自定义的异常日志记录工厂
    /// </summary>
    protected void UseExceptionLoggerFactory<TExceptionLoggerFactory>(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
    where TExceptionLoggerFactory : DefaultExceptionLoggerFactory, new()
    {
        if (Host == null) return;

        Host.ExceptionLoggerFactory = new TExceptionLoggerFactory()
            .SetLoggerFactory(Host.LoggerFactory)
            .SetLogLevel(logLevel);
    }

    #endregion
}