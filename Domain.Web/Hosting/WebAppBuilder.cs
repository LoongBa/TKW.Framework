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

public class WebAppBuilder<TUserInfo> : DomainAppBuilderBase<WebAppBuilder<TUserInfo>, DomainWebOptions, TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    internal WebAppBuilder(IDomainAppBuilderAdapter builder, DomainWebOptions options, List<Action<IApplicationBuilder>>? pipelineActions = null)
        : base(builder, options)
    {
        _PipelineActions = pipelineActions ?? [];
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));

        if (options.UseWebExceptionMiddleware)
        {
            _PipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());
        }
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