#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.Tools.Tags;

/// <summary>
/// 标签服务 (提供给业务层的统一门面)
/// </summary>
/// <remarks>
/// 构造函数注入了底层流水线，并允许指定全局的标签分隔符（默认为空格）
/// </remarks>
public class TagService(TagExtractionPipeline pipeline, string separator = " ")
{
    private readonly string _separator = separator;
    private List<TagRule> _Rules = [];

    /// <summary>
    /// 初始化/更新规则
    /// </summary>
    public void LoadRules(IEnumerable<TagRule>? rules)
    {
        _Rules = rules?.Where(r => r.IsEnabled).ToList() ?? [];
    }

    /// <summary>
    /// 版本1：获取文本的原始标签对象列表（已完成去重，供逻辑计算使用）
    /// </summary>
    /// <param name="text">输入文本</param>
    public IReadOnlyList<TagHit> GetTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _Rules.Count == 0)
            return Array.Empty<TagHit>();

        // 1. 将文本和内部持有的规则送入流水线处理
        // 注意：互斥组 (ExclusionGroup) 的优先级过滤应在 pipeline.Extract 内部完成
        var rawHits = pipeline.Extract(text, _Rules);

        // 2. 物理去重：确保同一个维度下的同一个标签只出现一次
        var uniqueHits = rawHits
            .DistinctBy(h => new { h.Dimension, h.TagName })
            .ToList();

        return uniqueHits;
    }

    /// <summary>
    /// 版本2：获取格式化后的标签字符串（自动清洗冲突字符，供数据库存储使用）
    /// </summary>
    /// <param name="text">输入文本</param>
    public string GetTagsString(string text)
    {
        // 内部复用 GetTags 以确保去重逻辑的一致性
        var hits = GetTags(text);
        if (hits.Count == 0)
            return string.Empty;

        // 3. 执行安全清洗，并按指定分隔符拼接成最终入库的字符串
        var formatted = hits.Select(h =>
            $"{Sanitize(h.Dimension)}:{Sanitize(h.TagName)}");

        return string.Join(_separator, formatted);
    }

    /// <summary>
    /// 安全清洗：确保维度名和标签名中绝对不包含设定的分隔符
    /// </summary>
    private string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 智能替换：如果使用空格作为分隔符，内容里的空格就转为下划线 (如 "双人 餐" -> "双人_餐")
        // 如果使用其他分隔符 (如 '|'或',')，则将其替换为安全的连字符 '-'
        var replacement = _separator == " " ? "_" : "-";

        return input.Replace(_separator, replacement);
    }
}