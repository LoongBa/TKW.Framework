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
    /// 检查元数据是否变更（需要重新生成）
    /// </summary>
    public bool NeedRegenerate(ClassMetadata metadata, string outputPath)
    {
        // 生成目标文件路径（如 TestService.generated.cs）
        var targetFilePath = fileWriter.ResolvePath(outputPath, $"{metadata.ClassName}.generated.cs");

        // 目标文件不存在 → 需要生成
        if (!fileWriter.Exists(targetFilePath))
            return true;

        // 计算元数据哈希与现有文件哈希 → 不一致则需要生成
        var metadataHash = ComputeMetadataHash(metadata);
        var existingFileHash = ComputeFileHash(targetFilePath);

        return metadataHash != existingFileHash;
    }

    /// <summary>
    /// 计算元数据哈希（基于关键字段）
    /// </summary>
    public static string ComputeMetadataHash(ClassMetadata metadata)
    {
        // 拼接元数据关键字段（根据实际需求调整）
        var hashInput = $"{metadata.Namespace}.{metadata.ClassName};" +
                        string.Join(";", metadata.Methods.Select(m =>
                            $"{m.Name}:{m.ReturnType}:{string.Join(",", m.Parameters.Select(p => $"{p.Name}:{p.TypeName}"))}"
                        ));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 计算现有文件哈希
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var bytes = SHA256.HashData(stream);
        return Convert.ToHexString(bytes);
    }
}