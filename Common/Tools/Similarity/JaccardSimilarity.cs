using System;
using System.Collections.Generic;

namespace TKW.Framework.Tools.Similarity;

/// <summary>
/// 基于 Bi-Gram 的 Jaccard 相似度计算
/// 适合词序颠倒但内容相似的文本判断
/// </summary>
public class JaccardSimilarity : ISimilarityChecker
{
    public double Calculate(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.IsEmpty && target.IsEmpty) return 1.0;
        if (source.Length < 2 || target.Length < 2) return source.SequenceEqual(target) ? 1.0 : 0.0;

        // 提取 Bi-gram (两个相邻字符作为一个单元)
        var sourceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < source.Length - 1; i++)
        {
            sourceSet.Add(source.Slice(i, 2).ToString());
        }

        var intersectionCount = 0;
        var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < target.Length - 1; i++)
        {
            var gram = target.Slice(i, 2).ToString();
            if (targetSet.Add(gram) && sourceSet.Contains(gram))
            {
                intersectionCount++;
            }
        }

        var unionCount = sourceSet.Count + targetSet.Count - intersectionCount;
        return unionCount == 0 ? 0 : (double)intersectionCount / unionCount;
    }
}