using System;
using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Core.Models;

/// <summary>
/// 代码生成的结果信息
/// </summary>
public class GenerateResult
{
    /// <summary>
    /// 提取器的字符串配置（来源类型 -> 配置字符串，支持JSON或key=value格式）
    /// </summary>
    public Dictionary<string, string> ExtractorConfigs { get; set; }
        = new();

    /// <summary>
    /// 生成是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 成功生成的文件信息（类名 -> 输出路径）
    /// </summary>
    public Dictionary<string, string> GeneratedFiles { get; } = new();

    /// <summary>
    /// 提取的元数据统计（来源 -> 数量）
    /// </summary>
    public Dictionary<string, int> ExtractedCounts { get; } = new();

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// 警告信息列表
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// 跳过的文件数量（增量生成中未变更的文件）
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 生成总耗时（毫秒）
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 成功生成的文件总数
    /// </summary>
    public int GeneratedCount => GeneratedFiles.Count;

    /// <summary>
    /// 提取的元数据总数
    /// </summary>
    public int TotalExtractedCount => ExtractedCounts.Values.Sum();

    /// <summary>
    /// 添加成功生成的文件记录
    /// </summary>
    public void AddGenerated(string className, string outputPath)
    {
        GeneratedFiles.TryAdd(className, outputPath);
    }

    /// <summary>
    /// 添加提取器的元数据统计
    /// </summary>
    public void AddExtracted(string sourceType, int count)
    {
        if (!ExtractedCounts.TryAdd(sourceType, count))
            ExtractedCounts[sourceType] += count;
    }

    /// <summary>
    /// 添加错误信息
    /// </summary>
    public void AddError(string errorMessage)
    {
        Errors.Add($"[{DateTime.Now:HH:mm:ss}] 错误: {errorMessage}");
    }

    /// <summary>
    /// 添加警告信息
    /// </summary>
    public void AddWarning(string warningMessage)
    {
        Warnings.Add($"[{DateTime.Now:HH:mm:ss}] 警告: {warningMessage}");
    }

    /// <summary>
    /// 生成人类可读的结果摘要
    /// </summary>
    public string GetSummary()
    {
        var summary = new List<string>
        {
            $"生成完成，耗时 {ElapsedMilliseconds}ms",
            $"提取元数据: 共 {TotalExtractedCount} 条（{string.Join(", ", ExtractedCounts.Select(kv => $"{kv.Key}: {kv.Value}"))}）",
            $"生成文件: {GeneratedCount} 个，跳过: {SkippedCount} 个"
        };

        if (Warnings.Any())
            summary.Add($"警告: {Warnings.Count} 条");

        if (Errors.Any())
            summary.Add($"错误: {Errors.Count} 条");
        else if (Success)
            summary.Add("状态: 成功");

        return string.Join(Environment.NewLine, summary);
    }
}
