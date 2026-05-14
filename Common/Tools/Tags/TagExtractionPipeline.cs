using System;
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.Tools.Tags;

/// <summary>
/// 标签提取流水线
/// </summary>
public class TagExtractionPipeline
{
    private readonly ITokenizer _Tokenizer;
    private readonly Dictionary<TagMatchMode, ITagMatcher> _Matchers;
    private readonly IEnumerable<ITagPipelinePostProcessor> _PostProcessors;

    // 使用主构造函数注入依赖
    public TagExtractionPipeline(
        ITokenizer tokenizer,
        IEnumerable<ITagMatcher> matchers,
        IEnumerable<ITagPipelinePostProcessor> postProcessors)
    {
        _Tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _Matchers = matchers.ToDictionary(m => m.Mode);
        _PostProcessors = postProcessors ?? [];
    }

    /// <summary>
    /// 执行标签提取
    /// </summary>
    public IReadOnlyList<TagHit> Extract(string text, IEnumerable<TagRule> rules)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        // 1. 依然使用 List 承载坐标，但通过 ITokenizer 的回调来填充
        // 为了 .NET 10 的极致性能，这里的 List 可以在类级别做 Pool（对象池）优化
        var tokens = new List<TokenText>(capacity: 64);

        // 调用流式接口
        _Tokenizer.Tokenize(text, tokens.Add);

        var rawHits = new List<TagHit>();

        // 2. 匹配阶段：Matcher 现在可以安全地使用 IReadOnlyList<TokenText>
        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            if (_Matchers.TryGetValue(rule.MatchMode, out var matcher))
            {
                var hits = matcher.Match(text, tokens, rule);
                rawHits.AddRange(hits);
            }
        }

        // 3. 后置处理阶段：清洗、互斥、优先级过滤
        foreach (var processor in _PostProcessors)
        {
            processor.Process(rawHits);
        }

        return rawHits.AsReadOnly();
    }
}