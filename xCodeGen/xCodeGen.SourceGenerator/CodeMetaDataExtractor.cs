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
using xCodeGen.Core.Utilities;

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
                var allMetadatas = infos.Select(i => i.Metadata).ToList();

                GenerateProjectMetaContext(spc, allMetadatas, projectInfo, new MetadataChangeLog());

                foreach (var info in infos)
                {
                    try { GenerateMetaFile(spc, info.Metadata); }
                    catch (Exception ex) { ReportError(spc, $"生成 {info.Metadata.ClassName} 失败: {ex.Message}"); }
                }
                GenerateDebugLogFile(spc);
            });
        }

        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return null;
            if (!symbol.HasGenerateCodeAttribute()) return null;

            var rawMetadata = ConvertToRawMetadata(typeDecl, context.SemanticModel);
            var classMetadata = ConvertToClassMetadata(rawMetadata);

            return new ClassGenerationInfo
            {
                Metadata = classMetadata,
                GenerateMode = GetGenerateMode(symbol),
                TemplateName = DefaultTemplateName,
                SemanticModel = context.SemanticModel
            };
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
                    { "GenerateMode", GetGenerateMode(symbol) },
                    { "TemplateName", DefaultTemplateName },
                    { "BaseType", symbol.BaseType?.ToDisplayString() ?? "object" },
                    { "ImplementedInterfaces", symbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList() },
                    { "Methods", ExtractMethodMetadataList(symbol) },
                    { "Properties", ExtractPropertyMetadataList(symbol) },
                    { "Attributes", ExtractAttributeMetadataList(symbol.GetAttributes()) }
                }
            };
        }

        private (string Text, string Source) GetNodeSummary(ISymbol symbol, SyntaxNode node)
        {
            // 轨道 1: Semantic XML (最稳健，原样返回 XML 内容)
            string xml = symbol.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                int start = xml.IndexOf("<summary>");
                int end = xml.IndexOf("</summary>");
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

        private ClassMetadata ConvertToClassMetadata(RawMetadata rawMetadata)
        {
            var data = rawMetadata.Data;
            var meta = new ClassMetadata
            {
                Namespace = data["Namespace"] as string,
                ClassName = data["ClassName"] as string,
                FullName = data["FullName"] as string,
                Summary = data.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                Mode = data["GenerateMode"] as string,
                SourceType = rawMetadata.SourceType,
                TemplateName = data["TemplateName"] as string,
                IsRecord = data.TryGetValue("IsRecord", out var ir) && (bool)ir,
                BaseType = data["BaseType"] as string ?? string.Empty,
                ImplementedInterfaces = (data["ImplementedInterfaces"] as List<string>)?.ToList() ?? new List<string>(),
                Methods = ConvertToMethodMetadataList(data["Methods"] as List<Dictionary<string, object>>),
                Properties = ConvertToPropertyMetadataList(data["Properties"] as List<Dictionary<string, object>>)
            };

            if (data.TryGetValue("SummarySource", out var src))
                meta.GenerateCodeSettings["ClassSummarySource"] = src.ToString();

            return meta;
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
                .Where(m => !m.IsImplicitlyDeclared && !CodeAnalysisDiagnostics.IsSpecialMethod(m))
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
                        { "Parameters", method.Parameters.Select(p => ExtractParameterMetadata(p, syntax as MethodDeclarationSyntax)).ToList() }
                    };
                }).ToList();
        }

        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs) =>
            rawAttrs?.Select(a => new AttributeMetadata
            {
                TypeFullName = a["TypeFullName"] as string,
                Properties = a.TryGetValue("Properties", out var p) ? (Dictionary<string, object>)p : new Dictionary<string, object>(),
                ConstructorArguments = a.TryGetValue("ConstructorArguments", out var c) ? (List<object>)c : new List<object>()
            }).ToList() ?? new List<AttributeMetadata>();

        private List<PropertyMetadata> ConvertToPropertyMetadataList(List<Dictionary<string, object>> rawProps) =>
            rawProps?.Select(p => new PropertyMetadata { Name = p["Name"] as string, TypeName = p["Type"] as string, TypeFullName = p["TypeFullName"] as string, IsNullable = (bool)p["IsNullable"], Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty, Attributes = ConvertToAttributeMetadataList(p["Attributes"] as List<Dictionary<string, object>>) }).ToList() ?? new List<PropertyMetadata>();

        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods) =>
            rawMethods?.Select(m => new MethodMetadata { Name = m["Name"] as string, ReturnType = m["ReturnType"] as string, IsAsync = (bool)m["IsAsync"], Summary = m.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty, AccessModifier = m["AccessModifier"] as string, Parameters = ConvertToParameterMetadataList(m["Parameters"] as List<Dictionary<string, object>>) }).ToList() ?? new List<MethodMetadata>();

        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams) =>
            rawParams?.Select(p => new ParameterMetadata { Name = p["Name"] as string, TypeName = p["Type"] as string, IsNullable = (bool)p["IsNullable"], Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty }).ToList() ?? new List<ParameterMetadata>();

        private Dictionary<string, object> ExtractParameterMetadata(IParameterSymbol parameter, MethodDeclarationSyntax methodSyntax) =>
            new Dictionary<string, object> { { "Name", parameter.Name }, { "Type", parameter.Type.ToDisplayString() }, { "IsNullable", CodeAnalysisDiagnostics.IsNullable(parameter.Type) }, { "Summary", methodSyntax != null ? GetParamSummary(methodSyntax, parameter.Name) : string.Empty }, { "Attributes", ExtractAttributeMetadataList(parameter.GetAttributes()) } };

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
                var props = new Dictionary<string, object>();

                // 提取命名参数 (NamedArguments)
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
                    { "TypeFullName", attr.AttributeClass?.ToDisplayString() },
                    { "Properties", props },
                    { "ConstructorArguments", constructorArgs }
                };
            }).ToList();
        }

        private string GetParamSummary(MethodDeclarationSyntax method, string name) { var trivia = method.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)); if (trivia == default) return string.Empty; var xml = trivia.GetStructure() as DocumentationCommentTriviaSyntax; var param = xml?.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString().Equals("param", StringComparison.OrdinalIgnoreCase) && e.StartTag.Attributes.Any(a => a.ToString().Contains($"\"{name}\""))); return param != null ? CleanXmlText(param.Content.ToString()) : string.Empty; }

        private string CleanXmlText(string text) => text.Replace("///", "").Replace("/**", "").Replace("*/", "").Replace("*", "").Trim();

        private static string GetGenerateMode(INamedTypeSymbol symbol) { var attr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName); return attr?.NamedArguments.FirstOrDefault(arg => arg.Key == "Type").Value.Value?.ToString() ?? "Full"; }
    }
}