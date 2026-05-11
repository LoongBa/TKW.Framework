using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域主机初始化基类：适配 IServiceCollection。
/// </summary>
public abstract class DomainHostInitializerBase<TUserInfo, TOptions>: IDomainSystemSetup
    where TUserInfo : class, IUserInfo, new()
    where TOptions : DomainOptions, new()
{
    protected DomainHost<TUserInfo>? Host { get; private set; }

    /// <summary>
    /// 初始化领域服务容器（由 DomainHost.Initialize 调用）
    /// </summary>
    public DomainUserHelperBase<TUserInfo> InitializeDiContainer(
        IServiceCollection services,
        IConfiguration? configuration,
        TOptions options)
    {
        OnPreInitialize(options, configuration);
        RegisterInfrastructureInternal(services, configuration, options);
        return OnRegisterDomainServices(services, configuration);
    }

    protected virtual void OnPreInitialize(TOptions options, IConfiguration? configuration) { }

    private void RegisterInfrastructureInternal(IServiceCollection services, 
        IConfiguration? configuration, TOptions options)
    {
        var projectMetaContext 
            = OnRegisterInfrastructureServices(services, configuration, options);
        if (projectMetaContext == null)
            throw new DomainException(@"项目元数据上下文 ProjectMetaContext 不能为空：检查初始化构造器 OnRegisterInfrastructureServices() 返回值。");

        // 注册 IProjectMetaContext 供后续查询
        // 此时 Host 尚未创建，需要在 BindServiceProvider 中处理
        services.AddSingleton(projectMetaContext);
        // 自动注册 DomainService/DomainDataService/Controller：基于 SG 生成的注册方法
        RegisterGeneratedServices(services, projectMetaContext);
        // 使用 TryAdd 确保 PreserveExistingDefaults 逻辑：优先保留已有的自定义实现
        services.TryAddSingleton<ISessionManager<TUserInfo>, NoSessionManager<TUserInfo>>();

        if (options.EnableDomainLogging)
        {
            // 内部调用适配 IServiceCollection 的 UseLogger
            services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        }
    }

    protected abstract IProjectMetaContext OnRegisterInfrastructureServices(IServiceCollection services,
        IConfiguration? configuration, TOptions options);
    protected abstract DomainUserHelperBase<TUserInfo> OnRegisterDomainServices(IServiceCollection services, IConfiguration? configuration);

    protected virtual void OnServiceProviderBuilt(IServiceProvider sp) { }

    /// <summary>
    /// 核心回调：在 IServiceProvider 构建后同步状态。
    /// </summary>
    public async Task ServiceProviderBuiltCallbackAsync(IServiceProvider sp)
    {
        var root = DomainHost<TUserInfo>.Root;

        // 幂等保护：如果还没初始化，或者已经绑定过了，直接拦截
        // 防止下方的 ConfigGlobalFilters 和 OnServiceProviderBuilt 被重复调用
        if (root == null || root.IsBound) return;
        // 1. 绑定核心宿主
        Host = root;
        root.BindServiceProvider(sp);
        // 2. 基础过滤与框架初始化
        ConfigGlobalFilters(sp);
        OnServiceProviderBuilt(sp);
        // 3. 执行自举与系统就绪检查
        try
        {
            await OnEnsureSystemReadyAsync(sp);
            // 🌟 自举成功，标记完成，解除业务封锁
            root.IsBootstrapCompleted = true;
        }
        catch (SystemSetupRequiredException ex)
        {
            root.SetupException = ex;
            // 注意：这里 IsBootstrapCompleted 依然是 false，
            // 任何非 Setup 页面的业务调用都会被 EnsureReady() 拦截报错！
            throw; // 必须往外抛，让 DomainMauiApplication 捕获
        }
    }

    protected abstract Task OnEnsureSystemReadyAsync(IServiceProvider sp);
    /// <summary>由系统设置模块调用：当用户修改了系统设置后，重新验证系统就绪状态。</summary>
    public async Task<bool> ValidateSystemReadinessAsync()
    {
        if (Host == null)
            throw new DomainException("DomainHost 尚未绑定服务提供者。");
        try
        {
            // 重新调用子类的业务就绪检查逻辑
            await OnEnsureSystemReadyAsync(Host.ServiceProvider!);

            // 如果没有抛出异常，说明一切就绪，清除异常状态
            Host.SetupException = null;
            Host.IsBootstrapCompleted = true;   // 将状态变更为 true
            return true;
        }
        catch (SystemSetupRequiredException ex)
        {
            // 验证失败，更新异常信息（比如还有哪些项没填）
            Host.SetupException = ex;
            return false;
        }
    }

    internal void RegisterGeneratedServices(IServiceCollection services, IProjectMetaContext projectMetaContext)
    {
        // 自动注册领域服务：基于 SG 自动生成的注册方法（或返回列表，在这里完成注册）
        var registrations = projectMetaContext.GetServiceRegistrations();

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