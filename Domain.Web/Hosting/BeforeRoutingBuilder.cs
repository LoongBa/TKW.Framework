using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

/// <summary>
/// 路由前构建器
/// 负责配置 UseRouting 之前的标准中间件（如重定向、认证等）。
/// </summary>
public class BeforeRoutingBuilder(IDomainAppBuilderAdapter builder, DomainWebOptions options, List<Action<IApplicationBuilder>> pipelineActions)
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
    public RoutingBuilder UseCustomRouting(Action<IApplicationBuilder, DomainWebOptions> customAction)
    {
        options.HasRoutingPhase = true;
        pipelineActions.Add(app => customAction(app, options));
        return new RoutingBuilder(builder, options, pipelineActions);
    }
}