using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TKW.Framework.Domain.Web;

internal class RoutingWarningHostedService(
    ILogger<RoutingWarningHostedService> logger,
    DomainWebConfigurationOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        if (options.SuppressRoutingWarning || options.HasRoutingPhase)
            return Task.CompletedTask;

        logger.LogWarning("TKWF.Domain.Web 未发现显式的路由阶段配置...");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}