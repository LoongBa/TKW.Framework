using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public abstract class DomainHostInitializerBase<TUserInfo, TDomainHelper>(ILifetimeScope? rootScope) : IStartable
where TUserInfo: class, IUserInfo, new()
where TDomainHelper : DomainHelperBase<TUserInfo>
{
    protected DomainHostInitializerBase() : this(null) {}

    // Start 在容器 Build 时被调用（自动）
    public void Start()
    {
        // 回填 DomainHost 的容器/服务提供者
        DomainHost<TUserInfo>.Root?.InitializeAfterHostBuild(rootScope!);
    }

    /// <summary>
    /// 在 ConfigureContainer 阶段由宿主传入 containerBuilder/services/configuration，
    /// 实现者在此注册领域需要的服务并返回领域助手实例。
    /// </summary>
    public abstract TDomainHelper InitializeDiContainer(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration);
}