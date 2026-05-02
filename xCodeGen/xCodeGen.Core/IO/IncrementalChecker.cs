using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.IO;

/// <summary>
/// 增量生成校验器，基于逻辑指纹判断是否需要重新生成
/// </summary>
public class IncrementalChecker(IFileWriter fileWriter)
{
    // 统一入口：不论是 ClassMetadata 还是 IProjectMetaContext，都走这套逻辑
    public bool NeedRegenerate(object context, string outputDirectory, string itemName, string pattern, string templateContent, out string currentHash)
    {
        currentHash = ComputeUnifiedHash(context, templateContent);
        var targetFilePath = fileWriter.ResolveOutputPath(outputDirectory, itemName, pattern);

        if (!File.Exists(targetFilePath)) return true;

        try
        {
            using var reader = new StreamReader(targetFilePath);
            for (var i = 0; i < 20; i++) // 稍微放大范围到 20 行
            {
                var line = reader.ReadLine();
                if (line == null) break;
                // 统一大小写比对
                if (line.Contains("[xCodeGen.Hash:") && line.IndexOf(currentHash, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
        }
        catch { return true; }
        return true;
    }

    private string ComputeUnifiedHash(object context, string templateContent)
    {
        using var sha = SHA256.Create();
        var sb = new StringBuilder();

        // 1. 注入模板内容 (模板改动必须重发)
        sb.Append(templateContent.Replace("\r\n", "\n").Trim());

        // 2. 注入元数据特征
        if (context is IProjectMetaContext project)
        {
            // 项目级：所有元数据的 SourceHash 累加
            foreach (var m in project.AllMetadatas.OrderBy(x => x.FullName))
                sb.Append(m.SourceHash);
        }
        else if (context is ClassMetadata metadata)
        {
            // 实体级：直接使用 SG 算好的 SourceHash
            sb.Append(metadata.SourceHash);
        }

        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}