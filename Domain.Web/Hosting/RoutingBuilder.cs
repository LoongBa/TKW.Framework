using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Web.Hosting;

/// <summary>
/// 路由终结点映射构建器
/// 专门负责 MapControllers, MapHub 等终结点配置。
/// </summary>
public class RoutingBuilder(IDomainAppBuilderAdapter builder, DomainWebOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    /// <summary>
    /// 配置具体的终结点映射。
    /// 【调整点】：将方法名由 AfterRouting 改为更贴切的 MapEndpoints。
    /// </summary>
    public AfterRoutingBuilder AfterRouting(Action<IEndpointRouteBuilder, DomainWebOptions> action)
    {
        pipelineActions.Add(app => app.UseEndpoints(endpoints => action(endpoints, options)));
        return new AfterRoutingBuilder(builder, options, pipelineActions);
    }
}