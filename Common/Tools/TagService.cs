using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TKW.Framework.Common.Tools;

public enum TagMatchMode
{
    Contains = 0,    // 包含
    StartsWith = 1,  // 以...开头
    EndsWith = 2,    // 以...结尾
    Regex = 3,       // 正则表达式
    FullMatch = 4    // 全字匹配
}

public class TagRule
{
    public string Dimension { get; set; } = string.Empty; // 维度：渠道、时段、品类
    public string TagName { get; set; } = string.Empty;   // 标签名：美团、夜班
    public TagMatchMode MatchMode { get; set; }           // 匹配策略
    public string Pattern { get; set; } = string.Empty;   // 匹配关键字或正则表达式
    public bool IsEnabled { get; set; } = true;           // 是否启用
}
public class TagService
{
    private List<TagRule> _Rules = [];

    /// <summary>
    /// 初始化规则（从任何来源加载后传入）
    /// </summary>
    public void LoadRules(IEnumerable<TagRule> rules)
    {
        _Rules = rules?.Where(r => r.IsEnabled).ToList() ?? [];
    }

    /// <summary>
    /// 解析名称，返回标签集合
    /// </summary>
    /// <param name="input">项目名称或描述</param>
    /// <returns>标签数组，格式如 ["渠道:美团", "时段:夜班"]</returns>
    public List<string> GetTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        var results = new List<string>();

        foreach (var rule in _Rules)
        {
            if (IsMatch(input, rule))
            {
                // 建议存储格式：维度:标签名
                results.Add($"{rule.Dimension}:{rule.TagName}");
            }
        }

        return results.Distinct().ToList();
    }

    private bool IsMatch(string input, TagRule rule)
    {
        return rule.MatchMode switch
        {
            TagMatchMode.Contains => input.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            TagMatchMode.StartsWith => input.StartsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            TagMatchMode.EndsWith => input.EndsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            TagMatchMode.FullMatch => input.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            TagMatchMode.Regex => Regex.IsMatch(input, rule.Pattern, RegexOptions.IgnoreCase),
            _ => false
        };
    }
}