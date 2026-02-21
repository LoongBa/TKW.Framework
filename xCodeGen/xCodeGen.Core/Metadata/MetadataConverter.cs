using System.Collections.Generic;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace xCodeGen.Core.Metadata;

/// <summary>
/// 元数据转换器：将原始提取数据映射为抽象模型
/// </summary>
public class MetadataConverter(NamingService namingService) : IMetadataConverter
{
    private readonly NamingService _NamingService = namingService;

    /// <summary>
    /// 元数据转换器：将原始提取数据映射为抽象模型
    /// </summary>
    public ClassMetadata Convert(RawMetadata rawMetadata)
    {
        var data = rawMetadata.Data;

        // 1. 解析类级基础属性及新增的类型识别字段
        var @namespace = GetValue(data, "Namespace", string.Empty);
        var sourceClassName = GetValue(data, "ClassName", string.Empty);
        var fullName = GetValue(data, "FullName", string.Empty);
        var mode = GetValue(data, "GenerateMode", "Full");
        var summary = GetValue(data, "Summary", string.Empty);
        var baseType = GetValue(data, "BaseType", string.Empty);

        // 核心新增：解析是否为 Record 及具体类型种类
        var isRecord = data.TryGetValue("IsRecord", out var ir) && (bool)ir;
        var typeKind = GetValue(data, "TypeKind", "class");

        // 2. 解析方法元数据
        var methods = new List<MethodMetadata>();
        if (data.TryGetValue("Methods", out var methodsObj) && methodsObj is List<object> methodsList)
        {
            foreach (var m in methodsList)
            {
                if (m is Dictionary<string, object> methodDict)
                {
                    methods.Add(ConvertToMethodMetadata(methodDict));
                }
            }
        }

        // 3. 构建并返回 ClassMetadata 实例
        return new ClassMetadata
        {
            Namespace = @namespace,
            ClassName = sourceClassName,
            FullName = fullName,
            SourceType = rawMetadata.SourceType,
            Mode = mode,
            Summary = summary,
            BaseType = baseType,
            Methods = methods,
            IsRecord = isRecord,
            TypeKind = typeKind,
            GenerateCodeSettings = data // 保留原始数据以备不时之需
        };
    }

    private static MethodMetadata ConvertToMethodMetadata(Dictionary<string, object> dict)
    {
        var methodName = GetValue(dict, "MethodName", "UnknownMethod");
        var returnType = GetValue(dict, "ReturnType", "void");
        var methodSummary = GetValue(dict, "Summary", string.Empty); // 方法自身的注释
        var isAsync = dict.TryGetValue("IsAsync", out var ia) && bool.Parse(ia.ToString() ?? string.Empty);

        var parameters = new List<ParameterMetadata>();
        if (dict.TryGetValue("Parameters", out var pObj) && pObj is List<object> pList)
        {
            foreach (var p in pList)
            {
                if (p is Dictionary<string, object> pDict)
                {
                    parameters.Add(new ParameterMetadata
                    {
                        Name = GetValue(pDict, "ParameterName", "unnamed"),
                        TypeName = GetValue(pDict, "Type", "object"),
                        Summary = GetValue(pDict, "Summary", string.Empty), // 修正：从 pDict 获取参数注释
                        IsNullable = pDict.TryGetValue("IsNullable", out var n) && bool.Parse(n.ToString() ?? string.Empty)
                    });
                }
            }
        }

        return new MethodMetadata
        {
            Name = methodName,
            ReturnType = returnType,
            Summary = methodSummary, // 正确映射方法注释
            IsAsync = isAsync,
            Parameters = parameters
        };
    }

    private static string GetValue(Dictionary<string, object> dict, string key, string @default)
        => dict.TryGetValue(key, out var val) ? val.ToString() : @default;
}