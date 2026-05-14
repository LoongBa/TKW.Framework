#nullable enable
using System;

namespace TKW.Framework.Tools.Tags;

/// <summary>
/// 匹配模式
/// </summary>
public enum TagMatchMode
{
    Contains = 0,
    StartsWith = 1,
    EndsWith = 2,
    Regex = 3,
    FullMatch = 4,
    /// <summary>
    /// 基于分词的精确匹配 (例如词库中有"苹果"，输入"青苹果"不会命中)
    /// </summary>
    TokenExact = 5
}

/// <summary>
/// 标签规则定义
/// </summary>
public class TagRule
{
    public string Dimension { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public TagMatchMode MatchMode { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // --- 新增高级特性 ---

    /// <summary>优先级 (数字越大优先级越高)</summary>
    public int Priority { get; set; } = 0;

    /// <summary>互斥组ID。同一个输入中，同组标签仅保留优先级最高的一个</summary>
    public string? ExclusionGroup { get; set; }
}

/// <summary>
/// 标签命中结果
/// </summary>
public record TagHit(
    string Dimension,
    string TagName,
    string MatchedValue,
    int StartIndex,
    int Length,
    int Priority,
    string? ExclusionGroup);

/// <summary>
/// 极致性能的分词单元：仅存储坐标，不持有引用
/// </summary>
public readonly struct TokenText(int startIndex, int length)
{
    public int StartIndex { get; } = startIndex;
    public int Length { get; } = length;

    /// <summary>
    /// 按需获取 ReadOnlySpan，由调用方提供原始字符串
    /// </summary>
    public ReadOnlySpan<char> GetSpan(string source) => source.AsSpan(StartIndex, Length);
}