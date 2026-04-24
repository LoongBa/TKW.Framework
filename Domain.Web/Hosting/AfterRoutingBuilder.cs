using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Web.Hosting;

/// <summary>
/// 路由后构建器：支持终结点映射后的后续链式配置。
/// </summary>
public class AfterRoutingBuilder(IDomainAppBuilderAdapter builder, DomainWebOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebOptions> action)
    {
        pipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, options)));
        return this;
    }
}