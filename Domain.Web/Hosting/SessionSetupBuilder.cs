using Microsoft.AspNetCore.Builder;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

/// <summary>
/// 会话设置构建器
/// 用于在 UseWebSession 之后，路由配置之前的中间件挂载。
/// </summary>
public class SessionSetupBuilder(IDomainAppBuilderAdapter builder, DomainWebOptions options, List<Action<IApplicationBuilder>> pipelineActions)
{
    /// <summary>
    /// 配置路由之前的自定义中间件
    /// </summary>
    public BeforeRoutingBuilder BeforeRouting(Action<IApplicationBuilder, DomainWebOptions> action)
    {
        pipelineActions.Add(app => action(app, options));
        return new BeforeRoutingBuilder(builder, options, pipelineActions);
    }

    /// <summary>
    /// 极简模式：直接配置应用管道，不使用标准路由
    /// </summary>
    public void NoRouting(Action<IApplicationBuilder, DomainWebOptions> action)
    {
        options.HasRoutingPhase = true;
        pipelineActions.Add(app => action(app, options));
    }
}