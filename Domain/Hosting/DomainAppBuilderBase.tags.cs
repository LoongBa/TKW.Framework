using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TKW.Framework.Tools.Tags;
using TKW.Framework.Tools.Tags.Matchers;
using TKW.Framework.Tools.Tags.Processors;

namespace TKW.Framework.Domain.Hosting;

public abstract partial class DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
{
    #region TagService

    /// <summary>
    /// 通用版：使用默认组件启动标签服务
    /// </summary>
    public TSubBuilder UseTagService(IEnumerable<TagRule>? tagRules = null)
    {
        return UseTagService<DefaultTokenizer>(tagRules);
    }

    /// <summary>
    /// 泛型版：指定自定义分词器启动标签服务
    /// </summary>
    public TSubBuilder UseTagService<TTokenizer>(IEnumerable<TagRule>? tagRules = null)
        where TTokenizer : class, ITokenizer
    {
        Builder.Services.TryAddSingleton<ITokenizer, TTokenizer>();
        return RegisterTagCore(tagRules);
    }

    /// <summary>
    /// 实例版：使用特定分词器实例启动标签服务
    /// </summary>
    public TSubBuilder UseTagService(ITokenizer tokenizerInstance, IEnumerable<TagRule>? tagRules = null)
    {
        ArgumentNullException.ThrowIfNull(tokenizerInstance);
        Builder.Services.Replace(ServiceDescriptor.Singleton(tokenizerInstance));
        return RegisterTagCore(tagRules);
    }

    /// <summary>
    /// 专用 Matcher 扩展：为流水线添加特定的匹配策略
    /// </summary>
    public TSubBuilder AddTagMatcher<TMatcher>()
        where TMatcher : class, ITagMatcher
    {
        Builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITagMatcher, TMatcher>());
        return (TSubBuilder)this;
    }

    /// <summary>
    /// 内部私有方法：处理流水线和 Service 的核心注册逻辑（去重注册）
    /// </summary>
    private TSubBuilder RegisterTagCore(IEnumerable<TagRule>? tagRules = null)
    {
        // 1. 集中注册内置基础匹配器家族 (TryAddEnumerable 保证幂等性，防止重复注册)
        Builder.Services.TryAddEnumerable([
            ServiceDescriptor.Singleton<ITagMatcher, TokenExactMatcher>(),
            ServiceDescriptor.Singleton<ITagMatcher, ContainsMatcher>(),
            ServiceDescriptor.Singleton<ITagMatcher, RegexMatcher>(),
            ServiceDescriptor.Singleton<ITagMatcher, StartsWithMatcher>(),
            ServiceDescriptor.Singleton<ITagMatcher, EndsWithMatcher>()
        ]);

        // 2. 注册默认后置处理器（处理互斥、优先级去重等）
        Builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITagPipelinePostProcessor, ExclusionGroupProcessor>()
        );

        // 3. 注册引擎流水线
        Builder.Services.TryAddSingleton<TagExtractionPipeline>();

        // 4. 注册业务门面 TagService
        Builder.Services.TryAddSingleton<TagService>(sp =>
        {
            var pipeline = sp.GetRequiredService<TagExtractionPipeline>();
            var tagService = new TagService(pipeline);

            var effectiveRules = tagRules ?? Options.TagRules;
            tagService.LoadRules(effectiveRules);

            return tagService;
        });

        return (TSubBuilder)this;
    }

    #endregion
}