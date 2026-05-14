#nullable enable
using System;
using System.Buffers;

namespace TKW.Framework.Tools.Similarity;

/// <summary>
/// 基于莱文斯坦距离 (编辑距离) 的相似度计算
/// 纯 C# 实现，AOT 兼容，极致内存优化
/// </summary>
public class LevenshteinSimilarity : ISimilarityChecker
{
    public double Calculate(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.IsEmpty && target.IsEmpty) return 1.0;
        if (source.IsEmpty || target.IsEmpty) return 0.0;
        if (source.SequenceEqual(target)) return 1.0;

        // 保证 source 是较短的那个，以节省内存分配
        if (source.Length > target.Length)
        {
            var temp = source;
            source = target;
            target = temp;
        }

        var sourceLen = source.Length;
        var targetLen = target.Length;

        // 性能优化：短字符串使用栈内存，长字符串使用数组池，避免 GC 压力
        int[]? pooledArray = null;
        var distances = sourceLen <= 256
            ? stackalloc int[sourceLen + 1]
            : (pooledArray = ArrayPool<int>.Shared.Rent(sourceLen + 1)).AsSpan(0, sourceLen + 1);

        try
        {
            for (var i = 0; i <= sourceLen; i++) distances[i] = i;

            for (var j = 1; j <= targetLen; j++)
            {
                var previousDiagonal = distances[0];
                distances[0] = j;

                for (var i = 1; i <= sourceLen; i++)
                {
                    var previousDiagonalSave = distances[i];
                    if (source[i - 1] == target[j - 1])
                    {
                        distances[i] = previousDiagonal;
                    }
                    else
                    {
                        // 取增、删、改的最小值 + 1
                        distances[i] = Math.Min(
                            Math.Min(distances[i - 1], distances[i]),
                            previousDiagonal) + 1;
                    }
                    previousDiagonal = previousDiagonalSave;
                }
            }

            var distance = distances[sourceLen];
            return 1.0 - (double)distance / Math.Max(sourceLen, targetLen);
        }
        finally
        {
            if (pooledArray != null)
            {
                ArrayPool<int>.Shared.Return(pooledArray);
            }
        }
    }
}