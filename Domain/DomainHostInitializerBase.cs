using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using xCodeGen.Abstractions.Metadata;

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
        var projectMetaContext = OnRegisterInfrastructureServices(services, configuration, options);
        // 自动注册领域服务：基于 SG 自动生成的注册方法（或返回列表，在这里完成注册）
        var serviceRegistrations = projectMetaContext.GetServiceRegistrations();
        // 获取 SG 生成的元数据列表
        var registrations = projectMetaContext.GetServiceRegistrations();

        // 执行分流注册
        RegisterGeneratedServices(services, registrations);

        // 使用 TryAdd 确保 PreserveExistingDefaults 逻辑：优先保留已有的自定义实现
        services.TryAddSingleton<ISessionManager<TUserInfo>, NoSessionManager<TUserInfo>>();

        if (options.EnableDomainLogging)
        {
            // 内部调用适配 IServiceCollection 的 UseLogger
            services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        }
    }

    protected abstract IProjectMetaContext OnRegisterInfrastructureServices(IServiceCollection services,
        IConfiguration? configuration, DomainOptions options);
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
    internal void RegisterGeneratedServices(IServiceCollection services, IEnumerable<DomainServiceRegistration> registrations)
    {
        foreach (var reg in registrations)
        {
            switch (reg)
            {
                // 判定：只有 Controller 且 具备代理类时才开启 AOP
                case { Type: MetaType.Controller, ProxyType: not null, ServiceInterface: not null }:
                    services.AddAopService(reg.ServiceInterface, reg.Implementation, reg.ProxyType, typeof(TUserInfo));
                    break;
                // 判定：类型为 Controller，但没有代理类或没有接口，抛出异常提示错误的注册配置
                case { Type: MetaType.Controller }:
                    throw new DomainException($"Controller 类型必须具备代理类和接口：{reg.Implementation.FullName}");
                // 判定：Service 或 DataService，均采用 AsSelf 注册
                case { Type: MetaType.Service or MetaType.DataService }:
                    services.AddService(reg.Implementation);
                    break;
            }
        }
    }
    #region 全局领域过滤器方法

    protected void AddGlobalFilter(DomainFilterAttribute<TUserInfo> filter) => Host?.AddGlobalFilter(filter);

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

    protected void EnableAuthorityFilter() => Host?.AddGlobalFilter(new AuthorityFilterAttribute<TUserInfo>());
    protected void EnableDomainLogging(EnumDomainLogLevel level = EnumDomainLogLevel.Normal)
        => Host?.AddGlobalFilter(new LoggingFilterAttribute<TUserInfo>(level));

    #endregion
}