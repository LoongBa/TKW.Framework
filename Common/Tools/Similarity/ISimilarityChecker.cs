using System;

namespace TKW.Framework.Tools.Similarity;

/// <summary>
/// 文本相似度计算器接口
/// </summary>
public interface ISimilarityChecker
{
    /// <summary>
    /// 计算相似度，返回值范围 0.0 (完全不同) ~ 1.0 (完全相同)
    /// </summary>
    double Calculate(ReadOnlySpan<char> source, ReadOnlySpan<char> target);

    /// <summary>
    /// 判断是否满足相似度阈值
    /// </summary>
    bool IsDuplicate(ReadOnlySpan<char> source, ReadOnlySpan<char> target, double threshold = 0.8)
    {
        // 默认实现，减少子类样板代码
        return Calculate(source, target) >= threshold;
    }
}