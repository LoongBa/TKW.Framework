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
    protected bool IsDevelopment { get; private set; }
    protected string ConnectionString { get; private set; } = string.Empty;
    protected Dictionary<string, string> ConfigDictionary { get; private set; } = new();

    private DomainHost<TUserInfo>? Host { get; set; }

    /// <summary>
    /// 初始化领域服务容器
    /// </summary>
    public DomainUserHelperBase<TUserInfo> InitializeDiContainer(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        // 1. 第一次同步：获取来自表现层的初始设定
        SyncOptionsToProperties(options);

        // 2. 领域层守门员逻辑：允许领域层根据业务安全要求强制修正参数
        OnPreInitialize(options, configuration);

        // 3. 第二次同步：确保领域层在 OnPreInitialize 中所做的修改能反映到属性中
        SyncOptionsToProperties(options);

        // 注册基础设施与业务服务
        OnRegisterInfrastructureServices(containerBuilder, services, configuration, options);
        return OnRegisterDomainServices(containerBuilder, services, configuration);
    }

    private void SyncOptionsToProperties(DomainOptions options)
    {
        IsDevelopment = options.IsDevelopment;
        ConnectionString = options.ConnectionString;
        ConfigDictionary = options.ConfigDictionary;
    }

    /// <summary>
    /// 预初始化钩子（守门员）：
    /// 领域层可在此检查或强制覆盖 options 里的参数。
    /// </summary>
    protected virtual void OnPreInitialize(DomainOptions options, IConfiguration? configuration) { }

    protected virtual void OnRegisterInfrastructureServices(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        if (options.SessionManagerType != null)
        {
            containerBuilder.RegisterType(options.SessionManagerType)
                .As<ISessionManager<TUserInfo>>()
                .SingleInstance();
        }
        else
        {
            UseDefaultSessionManager(containerBuilder);
        }
        containerBuilder.UseLogger();
    }

    protected abstract DomainUserHelperBase<TUserInfo> OnRegisterDomainServices(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration);

    protected internal virtual void OnContainerBuilt(IContainer? container, IConfiguration? configuration, bool isExternalContainer = false) { }

    internal void ContainerBuiltCallback(DomainHost<TUserInfo> host, bool isExternalContainer = false)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        host.IsDevelopment = IsDevelopment;

        ConfigGlobalFilters(host.Container, host.Configuration);
        OnContainerBuilt(host.Container, host.Configuration, isExternalContainer);
    }

    protected virtual void ConfigGlobalFilters(IContainer? container, IConfiguration? configuration)
    {
        EnableAuthorityFilter();
        if (IsDevelopment)
            UseDefaultExceptionLoggerFactory();
    }

    #region 过滤器与辅助方法

    protected void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter) => Host?.AddGlobalFilter(filter);

    protected ContainerBuilder UseDefaultSessionManager(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterTypeReplaceable<ISessionManager<TUserInfo>, SessionManager<TUserInfo>>();
        return containerBuilder;
    }

    protected void EnableDomainLogging(EnumDomainLogLevel level = EnumDomainLogLevel.Normal)
        => Host?.AddGlobalFilter(new LoggingFilterAttribute<TUserInfo>(level));

    protected void EnableAuthorityFilter()
        => Host?.AddGlobalFilter(new AuthorityFilterAttribute<TUserInfo>());

    protected void UseDefaultExceptionLoggerFactory(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
        => UseExceptionLoggerFactory<DefaultExceptionLoggerFactory>(logLevel);

    protected void UseExceptionLoggerFactory<TExceptionLoggerFactory>(EnumDomainLogLevel logLevel = EnumDomainLogLevel.None)
    where TExceptionLoggerFactory : DefaultExceptionLoggerFactory, new()
    {
        Host!.ExceptionLoggerFactory = new TExceptionLoggerFactory()
            .SetLoggerFactory(Host.LoggerFactory).SetLogLevel(logLevel);
    }

    #endregion
}