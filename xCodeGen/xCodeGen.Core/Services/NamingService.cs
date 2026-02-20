using System;
using System.Collections.Generic;
using System.Linq;
using xCodeGen.Core.Configuration;

namespace xCodeGen.Core.Services;

/// <summary>
/// 命名服务：负责根据规则计算产物名称
/// </summary>
public class NamingService(List<NamingRule> rules)
{
    private readonly List<NamingRule> _Rules = rules ?? [];

    /// <summary>
    /// 根据源名称和产物类型计算目标名称
    /// </summary>
    /// <param name="sourceName">原始类名或方法名</param>
    /// <param name="artifactType">产物类型（如 Dto, Validator）</param>
    /// <returns>计算后的目标名称</returns>
    public string GetTargetName(string sourceName, string artifactType)
    {
        if (string.IsNullOrEmpty(sourceName)) return sourceName;

        // 查找匹配的命名规则，若无则使用默认模式
        var rule = _Rules.FirstOrDefault(r =>
            string.Equals(r.ArtifactType, artifactType, StringComparison.OrdinalIgnoreCase));

        var pattern = rule?.Pattern ?? "{Name}{ArtifactType}";

        // 替换占位符
        return pattern
            .Replace("{Name}", sourceName)
            .Replace("{ArtifactType}", artifactType);
    }
}