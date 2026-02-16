using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TKW.Framework.Domain.Web;

public class RegisterServicesBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    // 关键修改：统一使用 IApplicationBuilder 接口
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal RegisterServicesBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    public RegisterServicesBuilder RegisterServices(Action<IServiceCollection, DomainWebConfigurationOptions> action)
    {
        action(_Builder.Services, _Options);
        return this;
    }

    // 路由前阶段：操作 IApplicationBuilder
    public BeforeRoutingBuilder BeforeRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app => action(app, _Options));
        return new BeforeRoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    // 极简模式：操作 IApplicationBuilder，用户需自行处理 UseRouting/UseEndpoints
    public void NoRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> action)
    {
        _Options.HasRoutingPhase = true;
        _PipelineActions.Add(app => action(app, _Options));
    }
}

public class BeforeRoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal BeforeRoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    public RoutingBuilder UseAspNetCoreRouting(
        bool enableUseAuthorization = true,
        Action<AuthorizationOptions>? authOptions = null)
    {
        _Options.HasRoutingPhase = true;

        // 1. 注册路由中间件
        _PipelineActions.Add(app => app.UseRouting());

        // 2. 自动处理安全逻辑
        if (enableUseAuthorization)
        {
            // 注册身份验证服务，解决 IAuthenticationSchemeProvider 缺失问题
            // 虽然这里没有配置具体的方案（如 JWT 或 Cookie），但它会注入核心容器服务，防止崩溃
            _Builder.Services.AddAuthentication();

            // 注册授权服务
            if (authOptions != null)
                _Builder.Services.AddAuthorization(authOptions);
            else
                _Builder.Services.AddAuthorization();

            // 3. 按照 ASP.NET Core 标准顺序添加中间件
            // 必须先认证 (Authentication)，后授权 (Authorization)
            _PipelineActions.Add(app => app.UseAuthentication());
            _PipelineActions.Add(app => app.UseAuthorization());
        }

        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }

    public RoutingBuilder UseCustomRouting(Action<IApplicationBuilder, DomainWebConfigurationOptions> customAction)
    {
        _Options.HasRoutingPhase = true;
        _PipelineActions.Add(app => customAction(app, _Options));
        return new RoutingBuilder(_Builder, _Options, _PipelineActions);
    }
}

public class RoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal RoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    // 关键修改：参数类型改为 IEndpointRouteBuilder，内部执行桥接
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app =>
        {
            // 通过 UseEndpoints 桥接，使 action 能调用 MapGraphQL 等
            app.UseEndpoints(endpoints => action(endpoints, _Options));
        });
        return new AfterRoutingBuilder(_Builder, _Options, _PipelineActions);
    }
}

public class AfterRoutingBuilder
{
    private readonly IHostApplicationBuilder _Builder;
    private readonly DomainWebConfigurationOptions _Options;
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal AfterRoutingBuilder(
        IHostApplicationBuilder builder,
        DomainWebConfigurationOptions options,
        List<Action<IApplicationBuilder>> pipelineActions)
    {
        _Builder = builder;
        _Options = options;
        _PipelineActions = pipelineActions;
    }

    // 允许链式添加多个终结点配置
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebConfigurationOptions> action)
    {
        _PipelineActions.Add(app =>
        {
            app.UseEndpoints(endpoints => action(endpoints, _Options));
        });
        return this;
    }
}