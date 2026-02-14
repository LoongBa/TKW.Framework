using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TKW.Framework.Domain.Web;

/// <summary>
/// 服务注册构建器，用于应用程序启动阶段的服务配置
/// </summary>
public class RegisterServicesBuilder
{
    /// <summary>
    /// 主机构建器，用于配置应用程序的主机和服务
    /// </summary>
    private readonly IHostApplicationBuilder _Builder;

    /// <summary>
    /// 领域Web配置选项，存储应用程序的配置信息
    /// </summary>
    private readonly DomainWebConfigurationOptions _Options;

    /// <summary>
    /// 管道操作集合，用于存储中间件配置动作
    /// </summary>
    private readonly List<Action<WebApplication>> _PipelineActions;

    /// <summary>
    /// 初始化服务注册构建器的新实例
    /// </summary>
    /// <param name="builder">主机构建器</param>
    /// <param name="options">领域Web配置选项</param>
    /// <param name="pipelineActions">管道操作集合</param>
    internal RegisterServicesBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<WebApplication>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 注册服务到依赖注入容器
    /// </summary>
    /// <param name="action">服务注册操作</param>
    /// <returns>当前构建器实例，支持链式调用</returns>
    public RegisterServicesBuilder RegisterServices(Action<IServiceCollection, DomainWebConfigurationOptions> action)
    {
        action(_Builder.Services, _Options);
        return this;
    }

    /// <summary>
    /// 配置路由前的中间件管道
    /// </summary>
    /// <param name="action">路由前的配置操作</param>
    /// <returns>路由前构建器实例</returns>
    public BeforeRoutingBuilder BeforeRouting(Action<WebApplication, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => action(app, _Options));
        return new BeforeRoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    /// <summary>
    /// 跳过路由阶段配置
    /// </summary>
    public void NoRouting(Action<WebApplication, DomainWebConfigurationOptions> action)
    {
        // 1. 标记状态，防止后续框架自动重复注入路由
        _Options.HasRoutingPhase = true;

        // 2. 关键修复：必须将 action 存入管道操作集合，否则它不会执行
        _PipelineActions.Add(app => action(app, _Options));

        // 3. 结束链式调用（返回 void 是正确的）
    }
}

/// <summary>
/// 路由前构建器，用于配置路由中间件之前的管道
/// </summary>
public class BeforeRoutingBuilder
{
    /// <summary>
    /// 主机构建器，用于配置应用程序的主机和服务
    /// </summary>
    private readonly IHostApplicationBuilder _Builder;

    /// <summary>
    /// 领域Web配置选项，存储应用程序的配置信息
    /// </summary>
    private readonly DomainWebConfigurationOptions _Options;

    /// <summary>
    /// 管道操作集合，用于存储中间件配置动作
    /// </summary>
    private readonly List<Action<WebApplication>> _PipelineActions;

    /// <summary>
    /// 初始化路由前构建器的新实例
    /// </summary>
    /// <param name="builder">主机构建器</param>
    /// <param name="options">领域Web配置选项</param>
    /// <param name="pipelineActions">管道操作集合</param>
    internal BeforeRoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<WebApplication>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 使用标准 ASP.NET Core 路由管道
    /// </summary>
    /// <param name="enableUseAuthorization">是否自动注入 UseAuthorization 中间件，默认为 true</param>
    /// <returns>路由构建器实例</returns>
    public RoutingBuilder UseAspNetCoreRouting(bool enableUseAuthorization = true)
    {
        CheckRoutingState();
        _Options.HasRoutingPhase = true;

        // 1. 注册路由中间件
        _PipelineActions.Add(app => app.UseRouting());

        // 2. 自动注入授权中间件（Secure by Default）
        if (enableUseAuthorization)
            _PipelineActions.Add(app => app.UseAuthorization());

        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    /// <summary>
    /// 使用自定义路由配置
    /// </summary>
    /// <param name="customAction">自定义路由配置操作</param>
    /// <returns>路由构建器实例</returns>
    public RoutingBuilder UseCustomRouting(Action<WebApplication, DomainWebConfigurationOptions> customAction)
    {
        CheckRoutingState();
        _Options.HasRoutingPhase = true;

        _PipelineActions.Add(app => customAction(app, _Options));
        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    /// <summary>
    /// 检查路由状态
    /// </summary>
    private void CheckRoutingState()
    {
        // 可扩展检查逻辑
    }
}

/// <summary>
/// 路由构建器，作为路由配置的分水岭阶段
/// </summary>
public class RoutingBuilder
{
    /// <summary>
    /// 主机构建器，用于配置应用程序的主机和服务
    /// </summary>
    private readonly IHostApplicationBuilder _Builder;

    /// <summary>
    /// 领域Web配置选项，存储应用程序的配置信息
    /// </summary>
    private readonly DomainWebConfigurationOptions _Options;

    /// <summary>
    /// 管道操作集合，用于存储中间件配置动作
    /// </summary>
    private readonly List<Action<WebApplication>> _PipelineActions;

    /// <summary>
    /// 初始化路由构建器的新实例
    /// </summary>
    /// <param name="builder">主机构建器</param>
    /// <param name="options">领域Web配置选项</param>
    /// <param name="pipelineActions">管道操作集合</param>
    internal RoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<WebApplication>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 配置路由后的中间件管道
    /// </summary>
    /// <param name="action">路由后的配置操作</param>
    /// <returns>路由后构建器实例</returns>
    public AfterRoutingBuilder AfterRouting(Action<WebApplication, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => action(app, _Options));
        return new AfterRoutingBuilder(_Builder, _Options, _PipelineActions);
    }
}

/// <summary>
/// 路由后构建器，用于配置路由中间件之后的管道
/// </summary>
public class AfterRoutingBuilder
{
    /// <summary>
    /// 主机构建器，用于配置应用程序的主机和服务
    /// </summary>
    private readonly IHostApplicationBuilder _Builder;

    /// <summary>
    /// 领域Web配置选项，存储应用程序的配置信息
    /// </summary>
    private readonly DomainWebConfigurationOptions _Options;

    /// <summary>
    /// 管道操作集合，用于存储中间件配置动作
    /// </summary>
    private readonly List<Action<WebApplication>> _PipelineActions;

    /// <summary>
    /// 初始化路由后构建器的新实例
    /// </summary>
    /// <param name="builder">主机构建器</param>
    /// <param name="options">领域Web配置选项</param>
    /// <param name="pipelineActions">管道操作集合</param>
    internal AfterRoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<WebApplication>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 添加路由后的中间件配置
    /// </summary>
    /// <param name="action">路由后的配置操作</param>
    /// <returns>当前构建器实例，支持链式调用</returns>
    public AfterRoutingBuilder AfterRouting(Action<WebApplication, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => action(app, _Options));
        return this;
    }
}
