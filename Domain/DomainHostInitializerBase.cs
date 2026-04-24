using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机初始化基类：适配 IServiceCollection。
/// </summary>
public abstract class DomainHostInitializerBase<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    protected DomainHost<TUserInfo>? Host { get; private set; }

    /// <summary>
    /// 初始化领域服务容器（由 DomainHost.Initialize 调用）
    /// </summary>
    public DomainUserHelperBase<TUserInfo> InitializeDiContainer(
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        OnPreInitialize(options, configuration);
        RegisterInfrastructureInternal(services, configuration, options);
        return OnRegisterDomainServices(services, configuration);
    }

    protected virtual void OnPreInitialize(DomainOptions options, IConfiguration? configuration) { }

    private void RegisterInfrastructureInternal(IServiceCollection services, IConfiguration? configuration, DomainOptions options)
    {
        OnRegisterInfrastructureServices(services, configuration, options);

        // 使用 TryAdd 确保 PreserveExistingDefaults 逻辑：优先保留已有的自定义实现
        services.TryAddSingleton<ISessionManager<TUserInfo>, NoSessionManager<TUserInfo>>();

        if (options.EnableDomainLogging)
        {
            // 内部调用适配 IServiceCollection 的 UseLogger
            services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        }
    }

    protected abstract void OnRegisterInfrastructureServices(IServiceCollection services, IConfiguration? configuration, DomainOptions options);
    protected abstract DomainUserHelperBase<TUserInfo> OnRegisterDomainServices(IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 核心回调：在 IServiceProvider 构建后同步状态。
    /// </summary>
    public void ServiceProviderBuiltCallback(IServiceProvider sp)
    {
        if (DomainHost<TUserInfo>.Root == null) return;

        this.Host = DomainHost<TUserInfo>.Root;
        Host.BindServiceProvider(sp);

        ConfigGlobalFilters(sp);
        OnServiceProviderBuilt(sp);
    }

    protected virtual void OnServiceProviderBuilt(IServiceProvider sp) { }

    protected virtual void ConfigGlobalFilters(IServiceProvider sp)
    {
        EnableAuthorityFilter();
        if (Host != null)
        {
            // 适配原生 DI 的解析逻辑
            var exceptionFactory = sp.GetService<DefaultExceptionLoggerFactory>() ?? new DefaultExceptionLoggerFactory();
            Host.ExceptionLoggerFactory = exceptionFactory.SetLoggerFactory(Host.LoggerFactory);
        }
    }

    #region 辅助控制方法

    protected void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter) => Host?.AddGlobalFilter(filter);
    protected void EnableAuthorityFilter() => Host?.AddGlobalFilter(new AuthorityFilterAttribute<TUserInfo>());
    protected void EnableDomainLogging(EnumDomainLogLevel level = EnumDomainLogLevel.Normal)
        => Host?.AddGlobalFilter(new LoggingFilterAttribute<TUserInfo>(level));

    #endregion
}