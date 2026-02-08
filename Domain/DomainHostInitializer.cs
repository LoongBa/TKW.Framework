using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public abstract class DomainHostInitializerBase<TUserInfo, TDomainHelper>
where TUserInfo : class, IUserInfo, new()
where TDomainHelper : DomainHelperBase<TUserInfo>
{
    /// <summary>
    /// 配置依赖注入容器，添加应用程序特定的服务和设置
    /// </summary>
    /// <remarks>重写此方法以注册应用程序所需的自定义服务、模块或配置源。
    /// 此方法通常在应用程序启动期间调用，用于在构建依赖注入容器之前对其进行准备。</remarks>
    /// <param name="containerBuilder">容器构建器，用于注册依赖注入的组件和服务</param>
    /// <param name="services">服务描述符集合，可以向其中添加或配置服务</param>
    /// <param name="configuration">应用程序配置设置，用于配置服务和组件</param>

    protected abstract void InitializeContainer(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 注册领域服务，例如领域服务、领域事件处理器、领域模型等。这些服务通常是领域层的核心组件，直接支持业务逻辑实现。
    /// </summary>
    protected abstract TDomainHelper RegisterDomainServices(ContainerBuilder containerBuilder,
        IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 注册领域基础设施服务，例如数据库上下文、日志记录、缓存等。这些服务通常是领域层依赖的第三方组件。
    /// </summary>
    protected abstract void RegisterInfrastructureServices(ContainerBuilder containerBuilder, IServiceCollection services, IConfiguration? configuration);

    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    /// <remarks>重写此方法可以在容器完全构建后、但在使用容器解析服务之前执行自定义操作。
    /// 此方法适用于需要构建后配置的高级场景。</remarks>
    protected internal abstract void OnContainerBuilt(DomainHost<TUserInfo> host, bool isExternalContainer = false);

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
        // 初始化容器
        InitializeContainer(containerBuilder, services, configuration);
        // 注册领域基础设施服务
        RegisterInfrastructureServices(containerBuilder, services, configuration);
        // 注册领域服务
        return RegisterDomainServices(containerBuilder, services, configuration);
    }

    /// <summary>
    /// 在依赖注入容器构建完成后调用，以允许进行额外的配置或初始化。
    /// </summary>
    internal void ContainerBuiltCallback(DomainHost<TUserInfo> host, IContainer? container, bool isExternalContainer = false)
    {
        // 调用派生类的 OnContainerBuilt 方法，允许进行额外的配置或初始化
        OnContainerBuilt(host, isExternalContainer);
    }

}