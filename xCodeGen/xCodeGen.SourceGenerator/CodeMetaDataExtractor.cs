using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.SourceGenerator
{
    [Generator]
    public partial class CodeMetaDataExtractor : IMetaDataExtractor, IIncrementalGenerator
    {
        public MetadataSource SourceType => MetadataSource.Code;
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidateTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => CodeAnalysisDiagnostics.IsCandidateClass(node),
                    transform: (ctx, _) => ExtractGenerationInfo(ctx)
                )
                .Where(info => info != null);

            var projectInfoProvider = context.CompilationProvider
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select((pair, _) => new ProjectInfo(pair.Right, pair.Left));

            context.RegisterSourceOutput(candidateTypes.Collect().Combine(projectInfoProvider), (spc, combined) =>
            {
                var (infos, projectInfo) = combined;

                var groupedInfos = infos.GroupBy(i =>
                        (i.Metadata.FullName ?? $"{i.Metadata.Namespace}.{i.Metadata.ClassName}"), StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();

                var allMetadatas = groupedInfos.Select(i => i.Metadata).ToList();
                //System.Diagnostics.Debugger.Launch();
                EnrichAndHashMetadatas(ref allMetadatas);

                // 1. 生成 Meta 文件
                foreach (var entity in allMetadatas.Where(m => m.Type == MetaType.Entity || m.Type == MetaType.View))
                {
                    try
                    {
                        GenerateMetaFile(spc, entity);
                    }
                    catch (Exception ex) { ReportError(spc, $"生成 {entity.ClassName} 失败: {ex.Message}"); }
                }

                // 2. 生成 Decorator
                foreach (var controller in allMetadatas.Where(m => m.Type == MetaType.Controller))
                {
                    GenerateInterfaceFile(spc, controller);
                    GenerateDecoratorFile(spc, controller);
                }

                GenerateProjectMetaContext(spc, allMetadatas.ToArray(), projectInfo, new MetadataChangeLog());
            });
        }

        private ClassMetadata ConvertToClassMetadata(RawMetadata rawMetadata)
        {
            var data = rawMetadata.Data;
            return new ClassMetadata
            {
                Namespace = data["Namespace"] as string,
                ClassName = data["ClassName"] as string,
                FullName = data["FullName"] as string,
                Summary = data.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                SourceType = rawMetadata.SourceType,
                TemplateName = DefaultTemplateName,
                IsRecord = data.TryGetValue("IsRecord", out var ir) && (bool)ir,
                BaseType = data["BaseType"] as string ?? string.Empty,
                TypeKind = data["TypeKind"] as string ?? "Class",
                ImplementedInterfaces = (data["ImplementedInterfaces"] as List<string>)?.ToList() ?? new List<string>(),
                // 使用 GetRawList 确保安全转换
                Attributes = ConvertToAttributeMetadataList(GetRawList(data, "Attributes")),
                Methods = ConvertToMethodMetadataList(GetRawList(data, "Methods")),
                Properties = ConvertToPropertyMetadataList(GetRawList(data, "Properties"))
            };
        }

        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods)
        {
            return rawMethods?.Select(m => new MethodMetadata
            {
                Name = m["Name"] as string,
                ReturnType = m["ReturnType"] as string,
                IsAsync = (bool)m["IsAsync"],
                Summary = m.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                AccessModifier = m["AccessModifier"] as string,
                // 深度递归使用 GetRawList
                Parameters = ConvertToParameterMetadataList(GetRawList(m, "Parameters")),
                Attributes = ConvertToAttributeMetadataList(GetRawList(m, "Attributes"))
            }).ToList() ?? new List<MethodMetadata>();
        }

        private List<PropertyMetadata> ConvertToPropertyMetadataList(List<Dictionary<string, object>> rawProps)
        {
            return rawProps?.Select(p => new PropertyMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                TypeFullName = p["TypeFullName"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                Attributes = ConvertToAttributeMetadataList(GetRawList(p, "Attributes"))
            }).ToList() ?? new List<PropertyMetadata>();
        }

        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams)
        {
            return rawParams?.Select(p => new ParameterMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                Attributes = ConvertToAttributeMetadataList(GetRawList(p, "Attributes"))
            }).ToList() ?? new List<ParameterMetadata>();
        }

        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs)
        {
            return rawAttrs?.Select(a => new AttributeMetadata
            {
                Name = a["Name"] as string,
                TypeFullName = a["TypeFullName"] as string,
                Properties = a.TryGetValue("Properties", out var p) ? (Dictionary<string, object>)p : new Dictionary<string, object>(),
                ConstructorArguments = a.TryGetValue("ConstructorArguments", out var c) ? (List<object>)c : new List<object>()
            }).ToList() ?? new List<AttributeMetadata>();
        }

        private List<Dictionary<string, object>> GetRawList(Dictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var val) && val is List<Dictionary<string, object>> list)
                return list;
            return new List<Dictionary<string, object>>();
        }

        private (string Text, string Source) GetNodeSummary(ISymbol symbol, SyntaxNode node)
        {
            // 轨道 1: Semantic XML (最稳健，原样返回 XML 内容)
            var xml = symbol.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                var start = xml.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
                var end = xml.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
                if (start != -1 && end != -1)
                {
                    return (xml.Substring(start + 9, end - start - 9).Trim(), "Semantic XML");
                }
            }

            // 轨道 2: Syntax Trivia (备选)
            var documentation = node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .Select(t => t.GetStructure() as DocumentationCommentTriviaSyntax)
                .FirstOrDefault();

            if (documentation != null)
            {
                var summaryElement = documentation.Content
                    .OfType<XmlElementSyntax>()
                    .FirstOrDefault(e => e.StartTag.Name.ToString().Equals("summary", StringComparison.OrdinalIgnoreCase));

                if (summaryElement != null)
                    return (CleanXmlText(summaryElement.Content.ToString()), "Syntax Trivia");
            }

            return (string.Empty, "None");
        }

        private RawMetadata ConvertToRawMetadata(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                return new RawMetadata { SourceType = MetadataSource.Error };

            var (summary, source) = GetNodeSummary(symbol, typeDecl);

            return new RawMetadata
            {
                SourceId = symbol.Name,
                SourceType = MetadataSource.Code,
                Data = new Dictionary<string, object>
                {
                    { "Namespace", symbol.ContainingNamespace.ToString() },
                    { "ClassName", symbol.Name },
                    { "FullName", symbol.ToDisplayString() },
                    { "Summary", summary },
                    { "SummarySource", source },
                    { "IsRecord", typeDecl is RecordDeclarationSyntax },
                    { "TypeKind", symbol.TypeKind.ToString() },
                    { "BaseType", symbol.BaseType?.ToDisplayString() ?? "object" },
                    { "ImplementedInterfaces", symbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList() },
                    { "Methods", ExtractMethodMetadataList(symbol) },
                    { "Properties", ExtractPropertyMetadataList(symbol) },
                    { "Attributes", ExtractAttributeMetadataList(symbol.GetAttributes()) }
                }
            };
        }

        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return null;

            // 识别 DomainGenerateCode 特性或领域服务接口
            var hasGenerateAttr = symbol.HasGenerateCodeAttribute();
            var implementsDomainService = symbol.AllInterfaces.Any(i => i.ToDisplayString().IndexOf("IDomainService", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!hasGenerateAttr && !implementsDomainService &&
                !symbol.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) &&
                symbol.Name?.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            var rawMetadata = ConvertToRawMetadata(typeDecl, context.SemanticModel);
            var classMetadata = ConvertToClassMetadata(rawMetadata);

            // 初步判定类型：如果是 DomainGenerateCode 触发，默认为 Entity
            if (hasGenerateAttr)
            {
                classMetadata.Type = MetaType.Entity;
                var genAttrData = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DomainGenerateCodeAttribute.TypeFullName);
                if (genAttrData != null)
                {
                    // 检查是否为 View
                    if (genAttrData.NamedArguments.FirstOrDefault(kv => kv.Key == "IsView").Value.Value is bool isView && isView)
                    {
                        classMetadata.Type = MetaType.View;
                    }
                }
            }

            return new ClassGenerationInfo
            {
                Metadata = classMetadata,
                GenerateMode = GetGenerateMode(symbol),
                TemplateName = DefaultTemplateName,
                SemanticModel = context.SemanticModel
            };
        }

        /// <summary>
        /// 提取特性元数据列表
        /// </summary>
        /// <summary>
        /// 增强版特性元数据提取：确保类型安全的值获取
        /// </summary>
        private List<Dictionary<string, object>> ExtractAttributeMetadataList(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Select(attr =>
            {
                var typeFullName = attr.AttributeClass?.ToDisplayString();
                var name = attr.AttributeClass?.Name ?? string.Empty;

                // 1. 获取特性类的出厂默认值作为基座（通过反射）
                var props = GetAttributeDefaults(typeFullName);

                // 2. 提取显式声明的命名参数，覆盖默认值
                foreach (var arg in attr.NamedArguments)
                {
                    // 关键：保留原始值类型 (bool, int, string) 以便下游 Policy 准确判断
                    props[arg.Key] = arg.Value.Value ?? string.Empty;
                }

                // 提取构造函数参数 (可选，用于 IndexAttribute 等)
                var constructorArgs = attr.ConstructorArguments
                    .Select(a => a.Value ?? string.Empty)
                    .ToList();

                return new Dictionary<string, object>
                {
                    { "TypeFullName", typeFullName },
                    { "Name", name },
                    { "Properties", props },
                    { "ConstructorArguments", constructorArgs }
                };
            }).ToList();
        }

        private List<Dictionary<string, object>> ExtractPropertyMetadataList(INamedTypeSymbol symbol)
        {
            return symbol.GetMembers().OfType<IPropertySymbol>()
                .Select(prop =>
                {
                    var syntax = prop.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                    var (sum, _) = syntax != null ? GetNodeSummary(prop, syntax) : (string.Empty, "None");
                    return new Dictionary<string, object>
                    {
                        { "Name", prop.Name },
                        { "Type", prop.Type.ToDisplayString() },
                        { "TypeFullName", prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) },
                        { "IsNullable", CodeAnalysisDiagnostics.IsNullable(prop.Type) },
                        { "Summary", sum },
                        { "Attributes", ExtractAttributeMetadataList(prop.GetAttributes()) }
                    };
                }).ToList();
        }

        private List<Dictionary<string, object>> ExtractMethodMetadataList(INamedTypeSymbol symbol)
        {
            return symbol.GetMembers().OfType<IMethodSymbol>()
                .Where(m =>
                {
                    if (m.IsImplicitlyDeclared) return false;
                    // 确保不要过滤掉接口的 Ordinary 方法
                    if (symbol.TypeKind == TypeKind.Interface) return true;
                    return !CodeAnalysisDiagnostics.IsSpecialMethod(m);
                })
                .Select(method =>
                {
                    var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                    var (sum, _) = syntax != null ? GetNodeSummary(method, syntax) : (string.Empty, "None");
                    return new Dictionary<string, object>
                    {
                        { "Name", method.Name },
                        { "ReturnType", method.ReturnType.ToDisplayString() },
                        { "IsAsync", method.IsAsync },
                        { "Summary", sum },
                        { "AccessModifier", CodeAnalysisDiagnostics.GetAccessModifier(method.DeclaredAccessibility) },
                        { "Parameters", method.Parameters.Select(p => ExtractParameterMetadata(p, syntax as MethodDeclarationSyntax)).ToList() },
                        { "Attributes", ExtractAttributeMetadataList(method.GetAttributes()) }
                    };
                }).ToList();
        }

        private Dictionary<string, object> ExtractParameterMetadata(IParameterSymbol parameter, MethodDeclarationSyntax methodSyntax)
        {
            return new Dictionary<string, object>
            {
                { "Name", parameter.Name },
                { "Type", parameter.Type.ToDisplayString() },
                { "IsNullable", CodeAnalysisDiagnostics.IsNullable(parameter.Type) },
                { "Summary", methodSyntax != null ? GetParamSummary(methodSyntax, parameter.Name) : string.Empty },
                { "Attributes", ExtractAttributeMetadataList(parameter.GetAttributes()) }
            };
        }
    }
}