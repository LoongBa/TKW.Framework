using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Web.Middlewares;
using TKW.Framework.Domain.Web.Session;

namespace TKW.Framework.Domain.Web.Hosting;

public class WebAppBuilder<TUserInfo, TOptions> : DomainAppBuilderBase<WebAppBuilder<TUserInfo, TOptions>, TOptions, TUserInfo>
    where TUserInfo : class, IUserInfo, new()
    where TOptions : DomainWebOptions, new()
{
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal WebAppBuilder(IDomainAppBuilderAdapter builder, TOptions options, List<Action<IApplicationBuilder>>? pipelineActions = null)
        : base(builder, options)
    {
        _PipelineActions = pipelineActions ?? [];
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));
        // 1. 注册统一异常处理中间件
        if (options.UseWebExceptionMiddleware) 
            _PipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());

        _PipelineActions.Add(app => app.Use(async (context, next) =>
        {
            var webOptions = context.RequestServices.GetRequiredService<IOptions<TOptions>>().Value;

            // 1. 检查开关：如果关闭了自动重定向，则直接透传
            if (!webOptions.AutoRedirectToSetup)
            {
                await next();
                return;
            }

            var root = DomainHost<TUserInfo>.Root;
            if (root?.SetupException != null)
            {
                var path = context.Request.Path;

                // 2. 静态资源、Setup 页面与 API 路径放行
                if (path.StartsWithSegments(webOptions.SetupPath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) // 放行 API
                    || path.Value?.Contains(".") == true)
                {
                    await next();
                    return;
                }

                // 3. 执行重定向
                context.Response.Redirect(webOptions.SetupPath);
                return;
            }

            await next();
        }));
    }

    public SessionSetupBuilder NoSession()
    {
        UseSessionManager<NoSessionManager<TUserInfo>>();
        return new SessionSetupBuilder(Builder, Options, _PipelineActions);
    }

    public SessionSetupBuilder UseWebSession(Action<WebSessionOptions>? setupAction = null)
    {
        return UseWebSession<WebSessionManager<TUserInfo>>(setupAction);
    }

    public SessionSetupBuilder UseWebSession<TSessionManager>(Action<WebSessionOptions>? setupAction = null)
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        setupAction?.Invoke(Options.WebSession);
        Builder.Services.TryAddSingleton(typeof(IIdGenerator), Options.IIdGeneratorType);

        UseSessionManager<TSessionManager>();

        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(Options.WebSession));
        return new SessionSetupBuilder(Builder, Options, _PipelineActions);
    }
}