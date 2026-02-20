using System.Collections.Generic;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Services;

namespace xCodeGen.Core.Metadata;

/// <summary>
/// 元数据转换器：将原始提取数据映射为抽象模型
/// </summary>
public class MetadataConverter(NamingService namingService) : IMetadataConverter
{
    private readonly NamingService _NamingService = namingService;

    public ClassMetadata Convert(RawMetadata rawMetadata)
    {
        // 1. 解析类级基础属性
        var @namespace = GetValue(rawMetadata.Data, "Namespace", string.Empty);
        var sourceClassName = GetValue(rawMetadata.Data, "ClassName", string.Empty);
        var mode = GetValue(rawMetadata.Data, "GenerateMode", "Full");

        // 2. 解析方法元数据
        var methods = new List<MethodMetadata>();
        if (rawMetadata.Data.TryGetValue("Methods", out var methodsObj) && methodsObj is List<object> methodsList)
        {
            foreach (var m in methodsList)
            {
                if (m is Dictionary<string, object> methodDict)
                {
                    methods.Add(ConvertToMethodMetadata(methodDict));
                }
            }
        }

        return new ClassMetadata
        {
            Namespace = @namespace,
            ClassName = sourceClassName,
            SourceType = rawMetadata.SourceType,
            Mode = mode,
            Methods = methods
        };
    }

    private MethodMetadata ConvertToMethodMetadata(Dictionary<string, object> dict)
    {
        // 修正：从 methodDict 中提取正确的 MethodName 键
        var methodName = GetValue(dict, "MethodName", "UnknownMethod");
        var returnType = GetValue(dict, "ReturnType", "void");
        var isAsync = dict.TryGetValue("IsAsync", out var ia) && bool.Parse(ia.ToString());

        // 3. 解析参数元数据
        var parameters = new List<ParameterMetadata>();
        if (dict.TryGetValue("Parameters", out var pObj) && pObj is List<object> pList)
        {
            foreach (var p in pList)
            {
                if (p is Dictionary<string, object> pDict)
                {
                    parameters.Add(new ParameterMetadata
                    {
                        // 修正：从参数字典中提取正确的参数名
                        Name = GetValue(pDict, "ParameterName", "unnamed"),
                        TypeName = GetValue(pDict, "Type", "object"),
                        IsNullable = pDict.TryGetValue("IsNullable", out var n) && bool.Parse(n.ToString())
                    });
                }
            }
        }

        return new MethodMetadata
        {
            Name = methodName,
            ReturnType = returnType,
            IsAsync = isAsync,
            Parameters = parameters
        };
    }

    private static string GetValue(Dictionary<string, object> dict, string key, string @default)
        => dict.TryGetValue(key, out var val) ? val.ToString() : @default;
}