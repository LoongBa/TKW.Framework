using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Web.Middlewares;
using TKW.Framework.Domain.Web.Session;

namespace TKW.Framework.Domain.Web.Hosting;

public class WebAppBuilder<TUserInfo> : DomainAppBuilderBase<WebAppBuilder<TUserInfo>, DomainWebOptions>
where TUserInfo : class, IUserInfo, new()
{
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal WebAppBuilder(IDomainAppBuilderAdapter builder,
        DomainWebOptions options, List<Action<IApplicationBuilder>>? pipelineActions = null)
        : base(builder, options)
    {
        _PipelineActions = pipelineActions ?? [];
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));

        if (options.UseWebExceptionMiddleware)
        {
            _PipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());
        }
    }

    /// <summary>
    /// 明确宣告当前应用完全不使用会话特性（适用于纯后台服务、定时任务等）。
    /// 将注入 NoSessionManager 阻断任何状态读写行为。
    /// （注：如果是无状态 WebAPI 或 JWT 鉴权，建议使用 UseWebSession&lt;StatelessSessionManager&gt;()）
    /// </summary>
    public SessionSetupBuilder NoSession()
    {
        // 【核心修改】：使用严格的 NoSessionManager
        UseSessionManagerInternal<TUserInfo, NoSessionManager<TUserInfo>>();
        return new SessionSetupBuilder(Builder, Options, _PipelineActions);
    }

    public SessionSetupBuilder UseWebSession(Action<WebSessionOptions>? setupAction = null)
    {
        return UseWebSession<WebSessionManager<TUserInfo>>(setupAction);
    }

    public SessionSetupBuilder UseWebSession<TSessionManager>(Action<WebSessionOptions>? setupAction = null)
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        var webOptions = Options;
        setupAction?.Invoke(webOptions.WebSession);

        Builder.Services.TryAddSingleton(typeof(IIdGenerator), webOptions.IIdGeneratorType);

        // 调用基类的内部注册，安全且不会被覆盖
        UseSessionManagerInternal<TUserInfo, TSessionManager>();

        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.WebSession));
        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }

    public SessionSetupBuilder UseWebSession(ISessionManager<TUserInfo> instance, Action<WebSessionOptions>? setupAction = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var webOptions = Options;
        setupAction?.Invoke(webOptions.WebSession);

        Builder.Services.TryAddSingleton(typeof(IIdGenerator), webOptions.IIdGeneratorType);

        // 调用基类的内部注册，安全且不会被覆盖
        UseSessionManagerInternal(instance);

        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.WebSession));

        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }
}