using System;
using System.Collections.Generic;
using System.Linq;
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

        // 核心优化：如果是从固化 DLL 加载的现成对象，直接透传返回
        if (rawMetadata.Data.TryGetValue("Object", out var obj) && obj is ClassMetadata compiledMeta)
        {
            return compiledMeta;
        }

        // 否则：执行常规的原始字典解析逻辑（用于兼容其他提取器）
        var data = rawMetadata.Data;

        return new ClassMetadata
        {
            Namespace = GetValue(data, "Namespace", string.Empty),
            ClassName = GetValue(data, "ClassName", string.Empty),
            FullName = GetValue(data, "FullName", string.Empty),
            Summary = GetValue(data, "Summary", string.Empty),
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