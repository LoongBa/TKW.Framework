using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TKW.Framework.Domain.Web;

/// <summary>
/// 路由阶段警告服务
/// 当开发者未显式调用 UseAspNetCoreRouting / UseCustomRouting / SkipRouting 时，
/// 在应用启动时输出警告日志，提醒可能遗漏路由配置。
/// </summary>
internal class RoutingWarningHostedService(
    ILogger<RoutingWarningHostedService> logger,
    DomainWebConfigurationOptions options)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 已显式选择路由阶段，或主动关闭警告 → 直接返回，不输出
        if (options.HasRoutingPhase || options.SuppressRoutingWarning)
            return Task.CompletedTask;

        // 未显式选择路由阶段，且未关闭警告 → 输出详细提示
        logger.LogWarning(
            """
            TKWF.Domain.Web 未显式选择路由阶段配置（UseAspNetCoreRouting / UseCustomRouting / SkipRouting）。
            这可能导致路由未注册或管道顺序异常，常见表现为：
            - 404 Not Found（路由未生效）
            - CORS 预检失败
            - 认证/授权中间件失效

            推荐处理方式（任选其一）：
            1. 使用标准 ASP.NET Core 路由：
               .UseAspNetCoreRouting()
            2. 使用自定义路由逻辑（可手动插入 UseRouting 等）：
               .UseCustomRouting(app => { app.UseRouting(); ... })
            3. 显式跳过路由阶段（无路由需求时）：
               .NoRouting()
            4. 如果确实不需要路由，可关闭此警告：
               cfg.SuppressRoutingWarning = true;

            如需更多帮助，请参考 TKWFDomain设计文档.md 中的“管道阶段与路由处理”章节。
            """);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}