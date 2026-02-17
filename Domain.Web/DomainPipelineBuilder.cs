using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

/// <summary>
/// Web 专用服务注册构建器
/// 负责初始的服务注册和配置，是构建管道的起点。
/// </summary>
public class RegisterServicesBuilder : DomainPipelineBuilderBase<RegisterServicesBuilder>
{
    // 延迟执行的管道配置委托列表，用于存储在 Configure 阶段执行的中间件注册逻辑
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal RegisterServicesBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<IApplicationBuilder>> pipelineActions)
        : base(builder, options)
    {
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 强制开启领域会话。
    /// 此方法会注册 SessionManager 并将 SessionUserMiddleware 加入管道。
    /// </summary>
    /// <typeparam name="TSessionManager">具体的 SessionManager 实现类型</typeparam>
    /// <typeparam name="TUserInfo">用户信息类型</typeparam>
    /// <param name="setupAction">会话选项配置委托</param>
    /// <returns>会话设置构建器，用于配置路由前的中间件</returns>
    public SessionSetupBuilder UseDomainSession<TSessionManager, TUserInfo>(Action<DomainSessionOptions>? setupAction = null)
        where TSessionManager : class, ISessionManager<TUserInfo>
        where TUserInfo : class, IUserInfo, new()
    {
        // 获取 Web 特定的配置选项
        var webOptions = (DomainWebConfigurationOptions)Options;

        // 应用用户提供的会话配置
        setupAction?.Invoke(webOptions.Session);

        // 将具体的 SessionManager 实现注册到 DI 容器
        // 注意：此处注册为 Singleton（单例），以确保会话数据在整个应用生命周期内共享
        // 这也覆盖了 Initializer 中的默认注册值
        Builder.Services.AddSingleton<ISessionManager<TUserInfo>, TSessionManager>();

        // 将 SessionUserMiddleware 加入管道配置列表
        // 泛型 TUserInfo 在此处闭环，后续 Builder (SessionSetupBuilder) 无需再携带该泛型参数
        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.Session));

        // 返回下一个阶段的构建器
        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }
}


/// <summary>
/// 会话设置构建器
/// 位于 UseDomainSession 之后，用于配置路由之前的中间件。
/// </summary>
public class SessionSetupBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal SessionSetupBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 配置路由之前的中间件
    /// </summary>
    /// <param name="action">配置委托</param>
    /// <returns>路由前构建器</returns>
    public BeforeRoutingBuilder BeforeRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => action(app, _Options));
        return new BeforeRoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    /// <summary>
    /// 极简模式：不使用标准路由，直接配置应用管道。
    /// 调用此方法后，将无法使用 UseAspNetCoreRouting 或 UseEndpoints 相关的功能。
    /// </summary>
    /// <param name="action">最终的应用配置委托</param>
    public void NoRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        _Options.HasRoutingPhase = true;
        _PipelineActions.Add(app => action(app, _Options));
    }
}

/// <summary>
/// 路由前构建器
/// 用于配置 UseRouting 之前的中间件，如异常处理、HTTPS重定向、静态文件、认证等。
/// </summary>
public class BeforeRoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal BeforeRoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 启用 ASP.NET Core 标准路由和授权
    /// </summary>
    /// <param name="enableUseAuthorization">是否启用授权中间件</param>
    /// <param name="authOptions">授权策略配置委托</param>
    /// <returns>路由构建器，用于配置终结点映射</returns>
    public RoutingBuilder UseAspNetCoreRouting(bool enableUseAuthorization = true, Action<AuthorizationOptions>? authOptions = null)
    {
        _Options.HasRoutingPhase = true;

        // 1. 添加路由中间件
        _PipelineActions.Add(app => app.UseRouting());

        // 2. 如果启用授权，配置相关服务和中间件
        if (enableUseAuthorization)
        {
            // 注册身份验证服务
            _Builder.Services.AddAuthentication();

            // 注册授权服务
            if (authOptions != null)
                _Builder.Services.AddAuthorization(authOptions);
            else
                _Builder.Services.AddAuthorization();

            // 添加身份验证中间件（必须在 UseRouting 之后，UseAuthorization 之前）
            _PipelineActions.Add(app => app.UseAuthentication());
            // 添加授权中间件
            _PipelineActions.Add(app => app.UseAuthorization());
        }

        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    /// <summary>
    /// 使用自定义路由逻辑
    /// </summary>
    /// <param name="customAction">自定义路由配置委托</param>
    /// <returns>路由构建器</returns>
    public RoutingBuilder UseCustomRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> customAction)
    {
        _Options.HasRoutingPhase = true;
        _PipelineActions.Add(app => customAction(app, _Options));
        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }
}

/// <summary>
/// 路由构建器
/// 用于配置终结点映射，如 MapControllers, MapRazorPages, MapHub, MapGraphQL 等。
/// </summary>
public class RoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal RoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 配置路由终结点
    /// </summary>
    /// <param name="action">终结点配置委托</param>
    /// <returns>路由后构建器，支持链式配置</returns>
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, _Options)));
        return new AfterRoutingBuilder(_Builder, _Options, _PipelineActions);
    }
}

/// <summary>
/// 路由后构建器
/// 用于在终结点配置之后继续添加配置，支持链式调用。
/// </summary>
public class AfterRoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal AfterRoutingBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options, List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    /// <summary>
    /// 继续添加终结点配置
    /// </summary>
    /// <param name="action">终结点配置委托</param>
    /// <returns>当前构建器实例，支持链式调用</returns>
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, _Options)));
        return this;
    }
}