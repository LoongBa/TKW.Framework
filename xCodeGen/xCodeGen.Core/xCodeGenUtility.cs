using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core;

/// <summary>
/// 共享工具类
/// </summary>
public static class XCodeGenUtility
{
    // 特性名称常量（字符串匹配）
    public const string GenerateCodeAttributeName = "GenerateCode";
    public const string GenerateCodeAttributeFullName = "xCodeGen.Abstractions.GenerateCode";

    /// <summary>
    /// 生成元数据文件名
    /// </summary>
    public static string GetMetaFileName(string sourceFileName, MetadataSource sourceType)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourceFileName);
        return sourceType switch
        {
            MetadataSource.Code => $"{fileName}.Meta.cs",
            MetadataSource.Database => $"{fileName}Table.Meta.cs",
            _ => $"{fileName}.Meta.cs"
        };
    }

    /// <summary>
    /// 生成元数据类名
    /// </summary>
    public static string GetMetaClassName(string sourceClassName, MetadataSource sourceType)
    {
        return sourceType switch
        {
            MetadataSource.Code => $"{sourceClassName}Meta",
            MetadataSource.Database => $"{sourceClassName}TableMeta",
            _ => $"{sourceClassName}Meta"
        };
    }

    /// <summary>
    /// 检查是否为GenerateCode特性
    /// </summary>
    public static bool HasGenerateCodeAttribute(string attributeText)
    {
        return attributeText.Contains(GenerateCodeAttributeName) || 
               attributeText.Contains(GenerateCodeAttributeFullName);
    }

    /// <summary>
    /// 提取GenerateCode特性的Mode参数
    /// </summary>
    public static string GetGenerateCodeMode(string attributeText)
    {
        var match = Regex.Match(attributeText, $@"{GenerateCodeAttributeName}\(.*Mode\s*=\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : "Full";
    }

    /// <summary>
    /// 计算元数据哈希值（用于增量生成）
    /// </summary>
    public static string ComputeMetadataHash(RawMetadata metadata)
    {
        var inputBytes = Encoding.UTF8.GetBytes($"{metadata.SourceType}:{metadata.SourceId}:{SerializeData(metadata.Data)}");
        var bytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(bytes);
    }

    // 简单序列化数据字典（实际项目可使用System.Text.Json）
    private static string SerializeData(Dictionary<string, object> data)
    {
        return string.Join("|", data.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
