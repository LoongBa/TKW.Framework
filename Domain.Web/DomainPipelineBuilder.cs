using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TKW.Framework.Domain.Web;

public class DomainPipelineBuilder
{
    private readonly DomainWebConfigurationOptions _Options;

    // 维护一个有序的动作列表
    private readonly List<Action<WebApplication>> _PipelineActions = [];
    private RoutingMode _RoutingMode = RoutingMode.None;

    internal DomainPipelineBuilder(IHostApplicationBuilder builder, DomainWebConfigurationOptions options)
    {
        _Options = options;

        // 注册统一的 StartupFilter，引用当前 Builder 的动作列表
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));
    }

    public DomainPipelineBuilder BeforeRouting(Action<WebApplication> action)
    {
        _PipelineActions.Add(action);
        return this;
    }

    public DomainPipelineBuilder UseAspNetCoreRouting()
    {
        CheckRoutingState();
        _RoutingMode = RoutingMode.AspNetCore;
        _Options.HasRoutingPhase = true; // 标记已配置，消除警告

        _PipelineActions.Add(app => app.UseRouting());
        return this;
    }

    public DomainPipelineBuilder UseCustomRouting(Action<WebApplication> customAction)
    {
        CheckRoutingState();
        _RoutingMode = RoutingMode.Custom;
        _Options.HasRoutingPhase = true;

        _PipelineActions.Add(customAction);
        return this;
    }

    public DomainPipelineBuilder AfterRouting(Action<WebApplication> action)
    {
        _PipelineActions.Add(action);
        return this;
    }

    private void CheckRoutingState()
    {
        if (_RoutingMode != RoutingMode.None)
            throw new InvalidOperationException("UseAspNetCoreRouting 和 UseCustomRouting 不能重复调用或同时调用。");
    }

    private enum RoutingMode { None, AspNetCore, Custom }
}

// 统一管道过滤器：确保按 List 顺序执行
sealed class DomainPipelineFilter(List<Action<WebApplication>> actions) : IStartupFilter
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