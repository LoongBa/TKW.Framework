using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// V4 领域应用构建器基类：负责流式配置领域环境
/// </summary>
public abstract class DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>(
    IDomainAppBuilderAdapter builder,
    TOptions options)
    where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
    where TOptions : DomainOptions
    where TUserInfo : class, IUserInfo, new()
{
    protected readonly IDomainAppBuilderAdapter Builder = builder;
    protected readonly TOptions Options = options;

    /// <summary>
    /// 注册自定义服务
    /// </summary>
    public TSubBuilder RegisterServices(Action<IServiceCollection, TOptions> action)
    {
        action(Builder.Services, Options);
        return (TSubBuilder)this;
    }

    /// <summary>
    /// 修改领域配置
    /// </summary>
    public TSubBuilder Configure(Action<TOptions> action)
    {
        action(Options);
        return (TSubBuilder)this;
    }

    /// <summary>
    /// 配置会话管理器 (单例)
    /// </summary>
    public TSubBuilder UseSessionManager<TSessionManager>()
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        Builder.Services.Replace(ServiceDescriptor.Singleton<ISessionManager<TUserInfo>, TSessionManager>());
        return (TSubBuilder)this;
    }

    public TSubBuilder UseSessionManager(ISessionManager<TUserInfo> instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Builder.Services.Replace(ServiceDescriptor.Singleton(instance));
        return (TSubBuilder)this;
    }

    /// <summary>
    /// 配置异常日志处理工厂
    /// </summary>
    public TSubBuilder UseExceptionLogger<TExceptionLogger>()
        where TExceptionLogger : DefaultExceptionLoggerFactory
    {
        Builder.Services.Replace(ServiceDescriptor.Singleton<DefaultExceptionLoggerFactory, TExceptionLogger>());
        return (TSubBuilder)this;
    }

    public TSubBuilder UseExceptionLogger(DefaultExceptionLoggerFactory instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Builder.Services.Replace(ServiceDescriptor.Singleton(instance));
        return (TSubBuilder)this;
    }

    /// <summary>
    /// 使用标签服务并预加载规则
    /// </summary>
    public TSubBuilder UseTagService(IEnumerable<TagRule>? tagRules = null)
    {
        Builder.Services.AddSingleton<TagService>(_ =>
        {
            var tagService = new TagService();
            var effectiveRules = tagRules ?? Options.TagRules;
            tagService.LoadRules(effectiveRules);
            return tagService;
        });
        return (TSubBuilder)this;
    }
}