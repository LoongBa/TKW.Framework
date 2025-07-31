using System.Collections.Generic;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Metadata;

/// <summary>
/// 元数据转换器实现
/// </summary>
public class MetadataConverter : IMetadataConverter
{
    public ClassMetadata Convert(RawMetadata rawMetadata)
    {
        // 解析基础属性
        var @namespace = rawMetadata.Data.TryGetValue("Namespace", out var ns) ? ns.ToString() : string.Empty;
        var className = rawMetadata.Data.TryGetValue("ClassName", out var cn) ? cn.ToString() : string.Empty;
        var generateMode = rawMetadata.Data.TryGetValue("GenerateMode", out var gm) ? gm.ToString() : "Full";

        // 解析方法元数据
        var methods = new List<MethodMetadata>();
        if (rawMetadata.Data.TryGetValue("Methods", out var methodsObj) && methodsObj is List<object> methodsList)
        {
            foreach (var methodObj in methodsList)
            {
                if (methodObj is Dictionary<string, object> methodDict)
                {
                    methods.Add(ConvertToMethodMetadata(methodDict));
                }
            }
        }

        return new ClassMetadata()
        {
            Namespace = @namespace,
            ClassName = className,
            SourceType = rawMetadata.SourceType,
            Mode = generateMode,
            Methods = methods,
        };
    }

    /// <summary>
    /// 转换方法元数据
    /// </summary>
    private static MethodMetadata ConvertToMethodMetadata(Dictionary<string, object> methodDict)
    {
        var name = methodDict.TryGetValue("ClassName", out var n) ? n.ToString() : string.Empty;
        var returnType = methodDict.TryGetValue("ReturnType", out var rt) ? rt.ToString() : "void";
        var isAsync = methodDict.TryGetValue("IsAsync", out var ia) && bool.TryParse(ia.ToString(), out var asyncFlag) && asyncFlag;

        // 转换参数元数据
        var parameters = new List<ParameterMetadata>();
        if (methodDict.TryGetValue("Parameters", out var paramsObj) && paramsObj is List<object> paramsList)
        {
            foreach (var paramObj in paramsList)
                if (paramObj is Dictionary<string, object> paramDict)
                    parameters.Add(new ParameterMetadata
                    {
                        Name = paramDict.TryGetValue("ClassName", out var pName) ? pName.ToString() : string.Empty,
                        TypeName = paramDict.TryGetValue("Type", out var pt) ? pt.ToString() : "void",
                        IsNullable = paramDict.TryGetValue("IsNullable", out var pn) && bool.TryParse(pn.ToString(), out var nullableFlag) && nullableFlag
                    });
        }

        return new MethodMetadata()
        {
            Name = name,
            ReturnType = returnType,
            IsAsync = isAsync,
            Parameters = parameters,
        };
    }
}
