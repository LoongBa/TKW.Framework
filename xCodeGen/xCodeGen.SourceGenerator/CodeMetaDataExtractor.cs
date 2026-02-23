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

            var generateAttribute = symbol.GetGenerateAttribute(GenerateCodeAttribute.TypeFullName);
            var (_, templateName, _) = CodeAnalysisHelper.ExtractGenerateAttributeParams(generateAttribute);

            return new ClassGenerationInfo
            {
                Metadata = classMetadata,
                GenerateMode = GetGenerateMode(symbol),
                TemplateName = templateName ?? DefaultTemplateName,
                SemanticModel = context.SemanticModel
            };
        }

        private RawMetadata ConvertToRawMetadata(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
            {
                return new RawMetadata { SourceType = MetadataSource.Error };
            }

            bool isRecord = typeDecl is RecordDeclarationSyntax;
            bool isRecordStruct = isRecord && typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StructKeyword));
            string typeKind = isRecordStruct ? "record struct" : (isRecord ? "record" : typeDecl.Keyword.ValueText);

            var generateAttribute = symbol.GetGenerateAttribute(GenerateCodeAttribute.TypeFullName);
            var (_, templateName, _) = CodeAnalysisHelper.ExtractGenerateAttributeParams(generateAttribute);

            return new RawMetadata
            {
                SourceId = symbol.Name,
                SourceType = MetadataSource.Code,
                Data = new Dictionary<string, object>
                {
                    { "Namespace", symbol.ContainingNamespace.ToString() },
                    { "ClassName", symbol.Name },
                    { "FullName", symbol.ToDisplayString() },
                    { "Summary", GetNodeSummary(typeDecl) },
                    { "IsRecord", isRecord },
                    { "TypeKind", typeKind },
                    { "GenerateMode", GetGenerateMode(symbol) },
                    { "TemplateName", templateName ?? DefaultTemplateName },
                    { "BaseType", symbol.BaseType?.ToDisplayString() ?? "object" },
                    { "ImplementedInterfaces", symbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList() },
                    { "Methods", ExtractMethodMetadataList(symbol) },
                    { "Properties", ExtractPropertyMetadataList(symbol) }
                }
            };
        }

        private ClassMetadata ConvertToClassMetadata(RawMetadata rawMetadata)
        {
            var data = rawMetadata.Data;
            return new ClassMetadata
            {
                Namespace = data["Namespace"] as string,
                ClassName = data["ClassName"] as string,
                FullName = data["FullName"] as string,
                Summary = data.TryGetValue("Summary", out var s) ? s?.ToString() ?? "" : "",
                Mode = data["GenerateMode"] as string,
                SourceType = rawMetadata.SourceType,
                TemplateName = data["TemplateName"] as string,
                IsRecord = data.TryGetValue("IsRecord", out var ir) && (bool)ir,
                TypeKind = data.TryGetValue("TypeKind", out var tk) ? tk.ToString() : "class",
                BaseType = data["BaseType"] as string ?? string.Empty,
                ImplementedInterfaces = (data["ImplementedInterfaces"] as List<string>)?.ToList() ?? [],
                Methods = ConvertToMethodMetadataList(data["Methods"] as List<Dictionary<string, object>>),
                Properties = ConvertToPropertyMetadataList(data["Properties"] as List<Dictionary<string, object>>)
            };
        }

        #region 成员提取逻辑

        private List<Dictionary<string, object>> ExtractPropertyMetadataList(INamedTypeSymbol symbol)
        {
            return symbol.GetMembers().OfType<IPropertySymbol>()
                .Select(prop => {
                    var syntax = prop.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                    return new Dictionary<string, object>
                    {
                        { "Name", prop.Name },
                        { "Type", prop.Type.ToDisplayString() },
                        { "TypeFullName", prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) },
                        { "IsNullable", CodeAnalysisDiagnostics.IsNullable(prop.Type) },
                        { "Summary", syntax != null ? GetNodeSummary(syntax) : string.Empty },
                        { "Attributes", ExtractAttributeMetadataList(prop.GetAttributes()) }
                    };
                }).ToList();
        }

        private List<Dictionary<string, object>> ExtractMethodMetadataList(INamedTypeSymbol symbol)
        {
            return symbol.GetMembers().OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && !CodeAnalysisDiagnostics.IsSpecialMethod(m))
                .Select(method => {
                    var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                    return new Dictionary<string, object>
                    {
                        { "Name", method.Name },
                        { "ReturnType", method.ReturnType.ToDisplayString() },
                        { "IsAsync", method.IsAsync },
                        { "Summary", syntax != null ? GetNodeSummary(syntax) : string.Empty },
                        { "AccessModifier", CodeAnalysisDiagnostics.GetAccessModifier(method.DeclaredAccessibility) },
                        { "Parameters", method.Parameters.Select(p => ExtractParameterMetadata(p, syntax as MethodDeclarationSyntax)).ToList() }
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

        private List<Dictionary<string, object>> ExtractAttributeMetadataList(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Select(attr => new Dictionary<string, object>
            {
                { "TypeFullName", attr.AttributeClass?.ToDisplayString() },
                // 核心：直接存储 Value。如果是 Type，Value 是 ITypeSymbol
                { "Properties", attr.NamedArguments.ToDictionary(a => a.Key, a => a.Value.Value ?? string.Empty) }
            }).ToList();
        }

        #endregion

        #region 转换补全

        private List<PropertyMetadata> ConvertToPropertyMetadataList(List<Dictionary<string, object>> rawProps) =>
            rawProps?.Select(p => new PropertyMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                TypeFullName = p["TypeFullName"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? "" : "",
                Attributes = ConvertToAttributeMetadataList(p["Attributes"] as List<Dictionary<string, object>>)
            }).ToList() ?? new List<PropertyMetadata>();

        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods) =>
            rawMethods?.Select(m => new MethodMetadata
            {
                Name = m["Name"] as string,
                ReturnType = m["ReturnType"] as string,
                IsAsync = (bool)m["IsAsync"],
                Summary = m.TryGetValue("Summary", out var s) ? s?.ToString() ?? "" : "",
                AccessModifier = m["AccessModifier"] as string,
                Parameters = ConvertToParameterMetadataList(m["Parameters"] as List<Dictionary<string, object>>)
            }).ToList() ?? new List<MethodMetadata>();

        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams) =>
            rawParams?.Select(p => new ParameterMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? "" : "",
                Attributes = ConvertToAttributeMetadataList(p["Attributes"] as List<Dictionary<string, object>>)
            }).ToList() ?? new List<ParameterMetadata>();

        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs) =>
            rawAttrs?.Select(a => new AttributeMetadata
            {
                TypeFullName = a["TypeFullName"] as string,
                Properties = a.TryGetValue("Properties", out var p) ? (Dictionary<string, object>)p : new Dictionary<string, object>()
            }).ToList() ?? new List<AttributeMetadata>();

        #endregion

        #region XML 解析

        private string GetNodeSummary(SyntaxNode node)
        {
            var xmlTrivia = node.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            if (xmlTrivia == default) return string.Empty;
            var summary = (xmlTrivia.GetStructure() as DocumentationCommentTriviaSyntax)?.Content.OfType<XmlElementSyntax>()
                .FirstOrDefault(e => e.StartTag.Name.ToString().Equals("summary", StringComparison.OrdinalIgnoreCase));
            return summary != null ? CleanXmlText(summary.Content.ToString()) : string.Empty;
        }

        private string GetParamSummary(MethodDeclarationSyntax method, string name)
        {
            var trivia = method.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            if (trivia == default) return string.Empty;
            var xml = trivia.GetStructure() as DocumentationCommentTriviaSyntax;
            var param = xml?.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString().Equals("param", StringComparison.OrdinalIgnoreCase) && e.StartTag.Attributes.Any(a => a.ToString().Contains($"\"{name}\"")));
            return param != null ? CleanXmlText(param.Content.ToString()) : string.Empty;
        }

        private string CleanXmlText(string text) => text.Replace("///", "").Trim();

        #endregion

        private static string GetGenerateMode(INamedTypeSymbol symbol)
        {
            var attr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName);
            return attr?.NamedArguments.FirstOrDefault(arg => arg.Key == "Type").Value.Value?.ToString() ?? "Full";
        }
    }
}