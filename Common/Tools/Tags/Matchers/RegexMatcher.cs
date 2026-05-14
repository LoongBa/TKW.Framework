using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TKW.Framework.Tools.Tags.Matchers;

/// <summary>
/// 正则匹配器：支持复杂模式提取
/// 利用 .NET 原生正则缓存与 Span 零分配枚举
/// </summary>
public class RegexMatcher : ITagMatcher
{
    public TagMatchMode Mode => TagMatchMode.Regex;

    // 全局正则缓存：保证同一条规则的正则对象只编译一次
    // 线程安全，非常适合高并发的 TagService 单例环境
    private static readonly ConcurrentDictionary<string, Regex> _RegexCache = new();

    public IEnumerable<TagHit> Match(string text, IReadOnlyList<TokenText> tokens, TagRule rule)
    {
        if (string.IsNullOrEmpty(rule.Pattern) || string.IsNullOrEmpty(text))
        {
            return [];
        }

        // 从缓存获取或新建编译后的正则对象
        var regex = _RegexCache.GetOrAdd(rule.Pattern, pattern =>
            new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant));

        var results = new List<TagHit>();
        var span = text.AsSpan();

        // EnumerateMatches 是针对 Span 的高性能枚举器，不会产生任何 GC 堆分配
        foreach (var match in regex.EnumerateMatches(span))
        {
            results.Add(new TagHit(
                rule.Dimension,
                rule.TagName,
                text.Substring(match.Index, match.Length), // 仅在命中时截取原词
                match.Index,
                match.Length,
                rule.Priority,
                rule.ExclusionGroup
            ));
        }

        return results;
    }
}