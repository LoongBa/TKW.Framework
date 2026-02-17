using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

/// <summary>
/// Web 专用服务注册构建器（状态机起点）
/// 负责初始的服务注册和配置，是构建管道的起点。
/// </summary>
public class RegisterServicesBuilder : DomainPipelineBuilderBase<RegisterServicesBuilder>
{
    // 引用传递的管道配置委托列表。即使此时为空，后续链式调用也会持续填充该引用。
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    /// <summary>
    /// 初始化构建器：在此处完成 IStartupFilter 的注册和基础中间件的挂载。
    /// </summary>
    internal RegisterServicesBuilder(IHostApplicationBuilder builder, 
        DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>>? pipelineActions = null)
        : base(builder, options)
    {
        // 1. 创建共享的动作列表（这是所有中间件挂载的物理容器）
        _PipelineActions = pipelineActions ?? [];

        // 2. 在此处注册唯一的 IStartupFilter。
        // 这样做使得 WebApplicationExtensions 无需感知管道执行细节，只需关注注册。
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));

        // 3. 自动挂载全局异常中间件。
        // 确保 WebExceptionMiddleware 永远处于管道的最顶层，优先捕获后续所有组件的异常。
        if (options.UseDomainExceptionMiddleware)
        {
            _PipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());
        }
    }

    /// <summary>
    /// 强制开启领域会话。
    /// 此方法会注册 SessionManager 并将 SessionUserMiddleware 加入管道。
    /// </summary>
    /// <typeparam name="TSessionManager">具体的 SessionManager 实现类型</typeparam>
    /// <typeparam name="TUserInfo">用户信息类型</typeparam>
    /// <param name="setupAction">会话选项配置委托</param>
    /// <returns>会话设置构建器，用于配置路由前的中间件</returns>
    public SessionSetupBuilder UseDomainSession<TSessionManager, TUserInfo>(Action<WebSessionOptions>? setupAction = null)
        where TSessionManager : class, ISessionManager<TUserInfo>
        where TUserInfo : class, IUserInfo, new()
    {
        var webOptions = (DomainWebConfigurationOptions)Options;

        // 应用用户提供的会话配置
        setupAction?.Invoke(webOptions.WebSession);

        // 【重要】：将具体的 SessionManager 实现注册为 Singleton
        // 确保会话数据在整个 Web 应用生命周期内共享，解决内存版会话数据不一致问题。
        Builder.Services.AddSingleton<ISessionManager<TUserInfo>, TSessionManager>();

        // 将 SessionUserMiddleware 加入管道配置列表
        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.Session));

        // 返回下一个阶段的构建器
        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }
}

/// <summary>
/// 会话设置构建器
/// 用于在 UseDomainSession 之后，路由配置之前的中间件挂载。
/// </summary>
public class SessionSetupBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    /// <summary>
    /// 配置路由之前的自定义中间件
    /// </summary>
    public BeforeRoutingBuilder BeforeRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        pipelineActions.Add(app => action(app, options));
        return new BeforeRoutingBuilder(builder, options, pipelineActions);
    }

    /// <summary>
    /// 极简模式：直接配置应用管道，不使用标准路由
    /// </summary>
    public void NoRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        options.HasRoutingPhase = true;
        pipelineActions.Add(app => action(app, options));
    }
}

/// <summary>
/// 路由前构建器
/// 负责配置 UseRouting 之前的标准中间件（如重定向、认证等）。
/// </summary>
public class BeforeRoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    /// <summary>
    /// 启用 ASP.NET Core 标准路由和授权体系
    /// </summary>
    /// <param name="enableUseAuthorization">是否启用授权中间件</param>
    /// <param name="authOptions">授权策略配置委托</param>
    public RoutingBuilder UseAspNetCoreRouting(bool enableUseAuthorization = true, Action<AuthorizationOptions>? authOptions = null)
    {
        options.HasRoutingPhase = true;

        // 1. 添加路由中间件
        pipelineActions.Add(app => app.UseRouting());

        // 2. 配置认证与授权服务
        if (enableUseAuthorization)
        {
            builder.Services.AddAuthentication();

            if (authOptions != null)
                builder.Services.AddAuthorization(authOptions);
            else
                builder.Services.AddAuthorization();

            // 认证中间件必须在路由之后
            pipelineActions.Add(app => app.UseAuthentication());
            pipelineActions.Add(app => app.UseAuthorization());
        }

        return new RoutingBuilder(builder, options, pipelineActions);
    }

    /// <summary>
    /// 使用自定义路由逻辑
    /// </summary>
    public RoutingBuilder UseCustomRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> customAction)
    {
        options.HasRoutingPhase = true;
        pipelineActions.Add(app => customAction(app, options));
        return new RoutingBuilder(builder, options, pipelineActions);
    }
}

/// <summary>
/// 路由终结点映射构建器
/// 专门负责 MapControllers, MapHub 等终结点配置。
/// </summary>
public class RoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    /// <summary>
    /// 配置具体的终结点映射。
    /// 【调整点】：将方法名由 AfterRouting 改为更贴切的 MapEndpoints。
    /// </summary>
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        pipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, options)));
        return new AfterRoutingBuilder(builder, options, pipelineActions);
    }
}

/// <summary>
/// 路由后构建器：支持终结点映射后的后续链式配置。
/// </summary>
public class AfterRoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        pipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, options)));
        return this;
    }
}