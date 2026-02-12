using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace TKW.Framework.Domain.Web;

internal sealed class DomainPipelineFilter(List<Action<WebApplication>> actions) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var webApp = (WebApplication)app;
            // 严格按顺序执行用户配置的所有步骤
            foreach (var action in actions)
            {
                action(webApp);
            }
            next(app);
        };
    }
}