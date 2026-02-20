using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.IO;

/// <summary>
/// 增量生成校验器，基于元数据哈希判断是否需要重新生成
/// </summary>
public class IncrementalChecker(IFileWriter fileWriter)
{
    /// <summary>
    /// 检查特定产物是否需要重新生成
    /// </summary>
    /// <param name="metadata">类元数据</param>
    /// <param name="outputDirectory">输出子目录（如 Generated/Dtos）</param>
    /// <param name="targetName">计算后的产物名称（如 UserDto）</param>
    public bool NeedRegenerate(ClassMetadata metadata, string outputDirectory, string targetName)
    {
        // 1. 构建完整的目标物理路径
        var fileName = $"{targetName}.generated.cs";
        var targetFilePath = fileWriter.ResolveOutputPath(outputDirectory, targetName, "{ClassName}.generated.cs");

        // 2. 目标文件不存在 → 必须生成
        if (!File.Exists(targetFilePath))
            return true;

        // 3. 比较哈希值
        // 注意：理想情况下，哈希应包含：元数据内容 + 模板版本
        var metadataHash = ComputeMetadataHash(metadata);
        var existingFileHash = ComputeFileHash(targetFilePath);

        return metadataHash != existingFileHash;
    }

    /// <summary>
    /// 计算元数据哈希（确保只有关键结构变化时才触发重新生成）
    /// </summary>
    public static string ComputeMetadataHash(ClassMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.Append($"{metadata.Namespace}.{metadata.ClassName};");

        foreach (var method in metadata.Methods.OrderBy(m => m.Name))
        {
            sb.Append($"{method.Name}:{method.ReturnType};");
            foreach (var param in method.Parameters)
            {
                sb.Append($"{param.Name}:{param.TypeName}:{param.IsNullable};");
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch { return string.Empty; }
    }
}