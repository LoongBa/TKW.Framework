using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Utilities;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 基于Roslyn的代码元数据提取器（源生成器兼容版本）
    /// 职责：源生成器框架集成、元数据提取核心逻辑
    /// </summary>
    [Generator]
    public partial class CodeMetaDataExtractor : IMetaDataExtractor, IIncrementalGenerator
    {
        public MetadataSource SourceType => MetadataSource.Code;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. 筛选带有特性的类型声明（改为 TypeDeclarationSyntax 以支持 class 和 record）
            var candidateTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => CodeAnalysisDiagnostics.IsCandidateClass(node),
                    transform: (ctx, _) => ExtractGenerationInfo(ctx)
                )
                .Where(info => info != null);

            // 2. 项目信息提供器
            var projectInfoProvider = context.CompilationProvider
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select((pair, _) => new ProjectInfo(pair.Right, pair.Left));

            // 3. 注册生成输出
            context.RegisterSourceOutput(candidateTypes.Collect().Combine(projectInfoProvider), (spc, combined) =>
            {
                var (infos, projectInfo) = combined;
                var allMetadatas = infos.Select(i => i.Metadata).ToList();

                GenerateProjectMetaContext(spc, allMetadatas, projectInfo, new MetadataChangeLog());

                foreach (var info in infos)
                {
                    try
                    {
                        GenerateMetaFile(spc, info.Metadata);
                    }
                    catch (Exception ex)
                    {
                        ReportError(spc, $"生成 {info.Metadata.ClassName} 失败: {ex.Message}");
                    }
                }
                GenerateDebugLogFile(spc);
            });
        }

        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not { } symbol) return null;

            // 统一检查特性
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
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not { } symbol)
            {
                return new RawMetadata { SourceType = MetadataSource.Error };
            }

            // 识别 Record 和具体类别
            var isRecord = typeDecl is RecordDeclarationSyntax;
            var isRecordStruct = isRecord && typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StructKeyword));
            var typeKind = isRecordStruct ? "record struct" : (isRecord ? "record" : typeDecl.Keyword.ValueText);

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
                    { "Methods", ExtractMethodMetadataList(symbol) },
                    { "ImplementedInterfaces", symbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList() },
                    { "GenerateMode", GetGenerateMode(symbol) },
                    { "TemplateName", templateName ?? DefaultTemplateName },
                    { "BaseType", symbol.BaseType?.ToDisplayString() ?? "object" },
                    { "IsRecord", isRecord },
                    { "TypeKind", typeKind }
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
                Mode = data["GenerateMode"] as string,
                SourceType = rawMetadata.SourceType,
                TemplateName = data["TemplateName"] as string,
                IsRecord = data.TryGetValue("IsRecord", out var ir) && (bool)ir,
                TypeKind = data.TryGetValue("TypeKind", out var tk) ? tk.ToString() : "class",
                Methods = ConvertToMethodMetadataList(data["Methods"] as List<Dictionary<string, object>>),
                BaseType = data["BaseType"] as string ?? string.Empty,
                ImplementedInterfaces = (data["ImplementedInterfaces"] as List<string>)?.ToList() ?? []
            };
        }

        #region 补全的转换方法 (之前缺失的部分)

        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods)
        {
            if (rawMethods == null) return [];
            return rawMethods.Select(m => new MethodMetadata
            {
                Name = m["Name"] as string,
                ReturnType = m["ReturnType"] as string,
                IsAsync = (bool)m["IsAsync"],
                AccessModifier = m["AccessModifier"] as string,
                Parameters = ConvertToParameterMetadataList(m["Parameters"] as List<Dictionary<string, object>>)
            }).ToList();
        }

        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams)
        {
            if (rawParams == null) return [];
            return rawParams.Select(p => new ParameterMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                IsNullable = (bool)p["IsNullable"],
                Attributes = ConvertToAttributeMetadataList(p["Attributes"] as List<Dictionary<string, object>>)
            }).ToList();
        }

        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs)
        {
            if (rawAttrs == null) return [];
            return rawAttrs.Select(a => new AttributeMetadata
            {
                TypeFullName = a["TypeFullName"] as string
            }).ToList();
        }

        #endregion

        private List<Dictionary<string, object>> ExtractMethodMetadataList(INamedTypeSymbol classSymbol)
        {
            return classSymbol.GetMembers().OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && !CodeAnalysisDiagnostics.IsSpecialMethod(m))
                .Select(method => new Dictionary<string, object>
                {
                    { "Name", method.Name },
                    { "ReturnType", method.ReturnType.ToDisplayString() },
                    { "IsAsync", method.IsAsync },
                    { "AccessModifier", CodeAnalysisDiagnostics.GetAccessModifier(method.DeclaredAccessibility) },
                    { "Parameters", method.Parameters.Select(p => new Dictionary<string, object>
                    {
                        { "Name", p.Name },
                        { "Type", p.Type.ToDisplayString() },
                        { "IsNullable", CodeAnalysisDiagnostics.IsNullable(p.Type) },
                        { "Attributes", p.GetAttributes().Select(a => new Dictionary<string, object>
                        {
                            { "TypeFullName", a.AttributeClass?.ToDisplayString() }
                        }).ToList() }
                    }).ToList() }
                }).ToList();
        }

        private static string GetGenerateMode(INamedTypeSymbol classSymbol)
        {
            var attr = classSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName);
            var typeArg = attr?.NamedArguments.FirstOrDefault(arg => arg.Key == "Type");
            return typeArg?.Value.Value?.ToString() ?? "Full";
        }
    }
}