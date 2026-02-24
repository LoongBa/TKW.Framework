using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.IO;

/// <summary>
/// 增量生成校验器，基于元数据内容哈希判断是否需要重新生成（防抖动）
/// </summary>
public class IncrementalChecker(IFileWriter fileWriter)
{
    /// <summary>
    /// 检查特定产物是否需要重新生成
    /// </summary>
    /// <param name="metadata">类元数据</param>
    /// <param name="outputDirectory">输出子目录</param>
    /// <param name="className">类名（用于路径解析）</param>
    /// <param name="pattern">文件名模式（如 {ClassName}.Dto.generated.cs）</param>
    public bool NeedRegenerate(ClassMetadata metadata, string outputDirectory, string className, string pattern)
    {
        // 1. 构建完整的目标物理路径
        var targetFilePath = fileWriter.ResolveOutputPath(outputDirectory, className, pattern);

        // 2. 目标文件不存在 → 必须生成
        if (!File.Exists(targetFilePath))
            return true;

        // 3. 比较哈希值
        // 理想逻辑：只有元数据关键结构（属性、特性）变化时才触发重新生成
        var metadataHash = ComputeMetadataHash(metadata);
        var existingFileHash = ComputeFileHash(targetFilePath);

        return metadataHash != existingFileHash;
    }

    /// <summary>
    /// 计算元数据哈希（包含属性、特性，确保 Model 变更能被感知）
    /// </summary>
    public static string ComputeMetadataHash(ClassMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.Append($"{metadata.Namespace}.{metadata.ClassName};");

        // 属性变更（名称、类型、特性值）
        foreach (var prop in metadata.Properties.OrderBy(p => p.Name))
        {
            sb.Append($"{prop.Name}:{prop.TypeName};");
            foreach (var attr in prop.Attributes)
            {
                sb.Append($"[{attr.TypeFullName}({string.Join(",", attr.Properties.Keys)})];");
            }
        }

        // 方法变更
        foreach (var method in metadata.Methods.OrderBy(m => m.Name))
        {
            sb.Append($"{method.Name}:{method.ReturnType};");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            // 注意：这里计算的是文件内容的整体哈希
            // 若模板中包含时间戳，建议在生成的代码中固定 Hash 标记，仅读取标记行进行比对
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch { return string.Empty; }
    }
}