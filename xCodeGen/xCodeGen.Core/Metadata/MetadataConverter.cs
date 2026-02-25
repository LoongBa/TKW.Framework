using System;
using System.Collections.Generic;
using System.Linq;
using System.Net; // 核心：在此层执行转码
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Services;

namespace xCodeGen.Core.Metadata;

/// <summary>
/// 元数据转换器：将原始提取数据映射为强类型抽象模型
/// </summary>
public class MetadataConverter(NamingService namingService) : IMetadataConverter
{
    private readonly NamingService _NamingService = namingService;

    public ClassMetadata Convert(RawMetadata rawMetadata)
    {
        if (rawMetadata == null) throw new ArgumentNullException(nameof(rawMetadata));

        ClassMetadata metadata;

        // 处理透传对象或字典解析
        if (rawMetadata.Data.TryGetValue("Object", out var obj) && obj is ClassMetadata compiledMeta)
        {
            metadata = compiledMeta;
        }
        else
        {
            var data = rawMetadata.Data;
            metadata = new ClassMetadata
            {
                Namespace = GetValue(data, "Namespace", string.Empty),
                ClassName = GetValue(data, "ClassName", string.Empty),
                FullName = GetValue(data, "FullName", string.Empty),
                Summary = GetValue(data, "Summary", null),
                Mode = GetValue(data, "GenerateMode", "Full"),
                SourceType = rawMetadata.SourceType,
                TemplateName = GetValue(data, "TemplateName", "Default"),
                BaseType = GetValue(data, "BaseType", "object"),
                IsRecord = data.TryGetValue("IsRecord", out var ir) && ir is bool b && b,
                TypeKind = GetValue(data, "TypeKind", "class"),
                ImplementedInterfaces = (data.GetValueOrDefault("ImplementedInterfaces") as List<string>)?.ToList() ?? new List<string>(),
                Methods = ConvertToMethodMetadataList(data.GetValueOrDefault("Methods") as List<Dictionary<string, object>>),
                Properties = ConvertToPropertyMetadataList(data.GetValueOrDefault("Properties") as List<Dictionary<string, object>>)
            };
        }

        // 关键修复：执行清洗与 HTML 解码
        SanitizeMetadata(metadata);
        return metadata;
    }

    private void SanitizeMetadata(ClassMetadata meta)
    {
        /*
        meta.Summary = CleanAndDecode(meta.Summary);
        foreach (var p in meta.Properties)
        {
            p.Summary = CleanAndDecode(p.Summary);
        }
        foreach (var m in meta.Methods)
        {
            m.Summary = CleanAndDecode(m.Summary);
        }
    */
    }

    /// <summary>
    /// 解码 XML 实体并清除空白
    /// </summary>
    private static string? CleanAndDecode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // 核心修复点：将 &#x...; 还原为中文
        var decoded = WebUtility.HtmlDecode(input);
        return decoded.Trim();
    }

    private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods)
    {
        if (rawMethods == null) return new List<MethodMetadata>();
        return rawMethods.Select(m => new MethodMetadata
        {
            Name = GetValue(m, "Name", "Unknown"),
            ReturnType = GetValue(m, "ReturnType", "void"),
            IsAsync = m.TryGetValue("IsAsync", out var ia) && (bool)ia,
            Summary = GetValue(m, "Summary", string.Empty),
            AccessModifier = GetValue(m, "AccessModifier", "public"),
            Parameters = ConvertToParameterMetadataList(m.GetValueOrDefault("Parameters") as List<Dictionary<string, object>>)
        }).ToList();
    }

    private List<PropertyMetadata> ConvertToPropertyMetadataList(List<Dictionary<string, object>> rawProps)
    {
        if (rawProps == null) return new List<PropertyMetadata>();
        return rawProps.Select(p => new PropertyMetadata
        {
            Name = GetValue(p, "Name", "Unknown"),
            TypeName = GetValue(p, "Type", "object"),
            TypeFullName = GetValue(p, "TypeFullName", string.Empty),
            IsNullable = p.TryGetValue("IsNullable", out var n) && (bool)n,
            Summary = GetValue(p, "Summary", string.Empty),
            Attributes = ConvertToAttributeMetadataList(p.GetValueOrDefault("Attributes") as List<Dictionary<string, object>>)
        }).ToList();
    }

    private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams)
    {
        if (rawParams == null) return new List<ParameterMetadata>();
        return rawParams.Select(p => new ParameterMetadata
        {
            Name = GetValue(p, "Name", "unnamed"),
            TypeName = GetValue(p, "Type", "object"),
            IsNullable = p.TryGetValue("IsNullable", out var n) && (bool)n,
            Summary = GetValue(p, "Summary", string.Empty)
        }).ToList();
    }

    private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs)
    {
        if (rawAttrs == null) return new List<AttributeMetadata>();
        return rawAttrs.Select(a => new AttributeMetadata
        {
            TypeFullName = GetValue(a, "TypeFullName", string.Empty),
            Properties = a.GetValueOrDefault("Properties") as Dictionary<string, object> ?? new Dictionary<string, object>()
        }).ToList();
    }

    private static string GetValue(Dictionary<string, object> dict, string key, string @default)
        => dict.TryGetValue(key, out var val) ? val?.ToString() ?? @default : @default;
}