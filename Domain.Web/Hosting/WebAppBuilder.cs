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

/// <summary>
/// Web 专用服务注册构建器（状态机起点）
/// 负责初始的服务注册和配置，是构建管道的起点。
/// </summary>
public class WebAppBuilder<TUserInfo> : DomainAppBuilderBase<WebAppBuilder<TUserInfo>, DomainWebOptions>
where TUserInfo : class, IUserInfo, new()
{
    private readonly List<Action<IApplicationBuilder>> _PipelineActions;

    // 构造函数接收 IHostApplicationBuilder（WebApplicationBuilder 是其子类）
    internal WebAppBuilder(IDomainAppBuilderAdapter builder,
        DomainWebOptions options, List<Action<IApplicationBuilder>>? pipelineActions = null)
        : base(builder, options) // 传递给新的基类
    {
        _PipelineActions = pipelineActions ?? [];

        // 注册唯一的 IStartupFilter
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(_PipelineActions));

        // 自动挂载全局异常中间件
        if (options.UseWebExceptionMiddleware)
        {
            _PipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());
        }
    }
    /// <summary>
    /// 明确宣告不使用 Web 会话特性（适用于无状态 WebAPI 或纯 JWT 鉴权场景）。
    /// 将注入 StatelessSessionManager，阻断底层的本地存储兜底，并跳过中间件挂载。
    /// </summary>
    public SessionSetupBuilder NoSession()
    {
        // 显式注册无状态以占据坑位，同时不挂载 Web 中间件
        UseSessionManagerInternal<TUserInfo, StatelessSessionManager<TUserInfo>>(); 
        return new SessionSetupBuilder(Builder, Options, _PipelineActions);
    }
    /// <summary>
    /// 开启 Web 会话特性（使用默认的 WebSessionManager）
    /// 此方法会注册 TSessionManager 并将 SessionUserMiddleware 加入管道。
    /// </summary>
    public SessionSetupBuilder UseWebSession(Action<WebSessionOptions>? setupAction = null)
    {
        // 直接调用下面的泛型版本，传入默认的 WebSessionManager
        return UseWebSession<WebSessionManager<TUserInfo>>(setupAction);
    }
    /// <summary>
    /// 用指定的 SessionManager 开启领域会话。
    /// 此方法会注册 TSessionManager 并将 SessionUserMiddleware 加入管道。
    /// </summary>
    /// <typeparam name="TSessionManager">具体的 SessionManager 实现类型</typeparam>
    /// <typeparam name="TUserInfo">用户信息类型</typeparam>
    /// <param name="setupAction">会话选项配置委托</param>
    /// <returns>会话设置构建器，用于配置路由前的中间件</returns>
    public SessionSetupBuilder UseWebSession<TSessionManager>(Action<WebSessionOptions>? setupAction = null)
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        var webOptions = Options;

        // 应用用户提供的会话配置
        setupAction?.Invoke(webOptions.WebSession);

        // 注册 ID 生成器（即使没配置，也有 Options 里的默认值）
        // 使用 TryAddSingleton 防止重复注册，允许用户在外部先行注册自定义实现
        Builder.Services.TryAddSingleton(typeof(IIdGenerator), webOptions.IIdGeneratorType);
        // 将具体的 SessionManager 实现注册为 Singleton
        // 确保会话数据在整个 Web 应用生命周期内共享，解决内存版会话数据不一致问题。
        //Builder.Services.AddSingleton<ISessionManager<TUserInfo>, TSessionManager>();
        UseSessionManagerInternal<TUserInfo, TSessionManager>();

        // 将 SessionUserMiddleware 加入管道配置列表
        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.WebSession));

        // 返回下一个阶段的构建器
        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }
    /// <summary>
    /// 使用指定的 SessionManager 实例开启领域会话。
    /// 此方法会将传入的实例注册为单例，并将 SessionUserMiddleware 加入管道。
    /// </summary>
    /// <param name="instance">会话管理器实例</param>
    /// <param name="setupAction">会话选项配置委托</param>
    public SessionSetupBuilder UseWebSession(ISessionManager<TUserInfo> instance, Action<WebSessionOptions>? setupAction = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var webOptions = Options;
        setupAction?.Invoke(webOptions.WebSession);

        Builder.Services.TryAddSingleton(typeof(IIdGenerator), webOptions.IIdGeneratorType);

        // 核心差异：注册外部传入的实例
        //Builder.Services.AddSingleton(instance);
        UseSessionManagerInternal(instance);

        // 依然需要挂载 Web 中间件，否则前端无感
        _PipelineActions.Add(app => app.UseMiddleware<SessionUserMiddleware<TUserInfo>>(webOptions.WebSession));

        return new SessionSetupBuilder(Builder, webOptions, _PipelineActions);
    }
}