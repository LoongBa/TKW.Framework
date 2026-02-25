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
    public bool NeedRegenerate(ClassMetadata metadata, string outputDirectory, string className, string pattern, string templateContent, out string currentHash)
    {
        // 计算“逻辑指纹”
        currentHash = ComputeLogicHash(metadata, templateContent);

        var targetFilePath = fileWriter.ResolveOutputPath(outputDirectory, className, pattern);
        if (!File.Exists(targetFilePath)) return true;

        try
        {
            using var reader = new StreamReader(targetFilePath);
            // 扫描前 10 行寻找指纹标记
            for (int i = 0; i < 10; i++)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                // 核心对比：文件标记中是否包含当前逻辑指纹
                if (line.Contains("[xCodeGen.Hash:") && line.Contains(currentHash))
                    return false; // 指纹完全匹配，跳过生成
            }
        }
        catch { return true; }
        return true;
    }

    /// <summary>
    /// 计算逻辑指纹（元数据结构 + 模板持久化哈希）
    /// </summary>
    public static string ComputeLogicHash(ClassMetadata metadata, string templateContent)
    {
        var sb = new StringBuilder();
        // 1. 元数据维度
        sb.Append($"{metadata.Namespace}.{metadata.ClassName}:{metadata.Summary};");
        foreach (var p in metadata.Properties.OrderBy(p => p.Name))
        {
            sb.Append($"{p.Name}:{p.TypeName}:{p.Summary};");
            foreach (var a in p.Attributes.OrderBy(at => at.TypeFullName))
                sb.Append($"[{a.TypeFullName}];");
        }

        // 2. 模板维度：使用稳定哈希替代随机化的 GetHashCode()
        sb.Append($"|T:{GetStableHash(templateContent)}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).Substring(0, 16);
    }

    /// <summary>
    /// 获取稳定的字符串哈希（不受 .NET 运行时随机种子影响）
    /// </summary>
    private static string GetStableHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return "0";
        // 规范化换行符，防止因模板文件换行符不同导致哈希失效
        var normalized = input.Replace("\r\n", "\n").Replace("\r", "\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).Substring(0, 8);
    }
}