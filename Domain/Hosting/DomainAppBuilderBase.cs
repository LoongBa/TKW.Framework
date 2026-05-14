using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Tools.Tags;

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
    /// 使用标签服务并预加载规则（已更新为 V4 流水线架构）
    /// </summary>
    /// <param name="tagRules">可选的初始规则集。若为 null，则默认读取 Options.TagRules</param>
    public TSubBuilder UseTagService(IEnumerable<TagRule>? tagRules = null)
    {
        // 1. 注册核心组件
        // 使用 TryAddSingleton 允许用户通过在调用 UseTagService 之前注册自定义 ITokenizer 来替换默认实现
        Builder.Services.TryAddSingleton<ITokenizer, DefaultTokenizer>();

        // 注册内置匹配器（使用 AddEnumerable 支持多个 ITagMatcher 共存）
        Builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITagMatcher, TokenExactMatcher>());

        // 2. 注册流水线引擎
        Builder.Services.TryAddSingleton<TagExtractionPipeline>();

        // 3. 注册业务门面服务 TagService
        Builder.Services.AddSingleton<TagService>(sp =>
        {
            // 从容器中解析已经组装好的流水线（包含分词器和所有匹配器）
            var pipeline = sp.GetRequiredService<TagExtractionPipeline>();
            var tagService = new TagService(pipeline);

            // 规则装载优先级：显式参数 > Options 配置
            var effectiveRules = tagRules ?? Options.TagRules;
            tagService.LoadRules(effectiveRules);

            return tagService;
        });

        return (TSubBuilder)this;
    }
}