using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction;

/// <summary>
/// 基于 Roslyn 语义分析的元数据提取器实现
/// 优化：支持 class/record/struct 等多种类型声明
/// </summary>
public class RoslynExtractor : IMetaDataExtractor
{
    // 明确对应代码来源类型
    public MetadataSource SourceType => MetadataSource.Code;

    /// <summary>
    /// 扫描并提取目标项目中的元数据
    /// </summary>
    public async Task<IEnumerable<RawMetadata>> ExtractAsync(
        ExtractorOptions options,
        CancellationToken cancellationToken = default)
    {
        var rawMetadataList = new List<RawMetadata>();

        // 加载项目编译对象 (注意：执行此方法前需初始化 MSBuildLocator)
        var compilation = await CreateCompilationAsync(options.ProjectPath, cancellationToken);

        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(cancellationToken);

            // 核心修改：查找所有类型声明（涵盖 class, record, struct, interface）
            var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not { } symbol) continue;

                // 统一检查特性名称：GenerateCodeAttribute
                if (!symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "GenerateCodeAttribute"))
                    continue;

                rawMetadataList.Add(new RawMetadata
                {
                    SourceType = SourceType,
                    SourceId = symbol.ToDisplayString(),
                    Data = ExtractTypeData(symbol, typeDecl, semanticModel)
                });
            }
        }

        return rawMetadataList;
    }

    /// <summary>
    /// 提取类型级的详细元数据
    /// </summary>
    private Dictionary<string, object> ExtractTypeData(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax syntax,
        SemanticModel model)
    {
        var isRecord = IsRecordType(syntax, out var isRecordStruct);

        return new Dictionary<string, object>
        {
            ["Namespace"] = symbol.ContainingNamespace.ToDisplayString(),
            ["ClassName"] = symbol.Name,
            ["FullName"] = symbol.ToDisplayString(),
            ["IsRecord"] = isRecord,
            ["TypeKind"] = isRecordStruct ? "record struct" : (isRecord ? "record" : syntax.Keyword.Text),
            ["Summary"] = GetNodeSummary(syntax), // 提取类/Record注释
            ["BaseType"] = symbol.BaseType?.ToDisplayString() ?? string.Empty,
            ["Methods"] = symbol.GetMembers().OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary)
                .Select(m => ExtractMethodData(m, model)).ToList()
        };
    }

    /// <summary>
    /// 提取方法级的元数据
    /// </summary>
    private Dictionary<string, object> ExtractMethodData(IMethodSymbol methodSymbol, SemanticModel model)
    {
        var methodSyntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;

        return new Dictionary<string, object>
        {
            ["MethodName"] = methodSymbol.Name,
            ["ReturnType"] = methodSymbol.ReturnType.ToDisplayString(),
            ["IsAsync"] = methodSymbol.IsAsync,
            ["Summary"] = methodSyntax != null ? GetNodeSummary(methodSyntax) : string.Empty,
            ["Parameters"] = methodSymbol.Parameters.Select(p => ExtractParameterData(p, methodSyntax)).ToList()
        };
    }

    /// <summary>
    /// 提取参数级的元数据（语义化提取）
    /// </summary>
    private Dictionary<string, object> ExtractParameterData(IParameterSymbol parameterSymbol, MethodDeclarationSyntax methodSyntax)
    {
        return new Dictionary<string, object>
        {
            ["ParameterName"] = parameterSymbol.Name,
            ["Type"] = parameterSymbol.Type.ToDisplayString(),
            ["IsNullable"] = parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated ||
                             parameterSymbol.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
            ["IsCollection"] = parameterSymbol.Type.AllInterfaces.Any(i => i.Name == "IEnumerable") ||
                               parameterSymbol.Type.TypeKind == TypeKind.Array,
            ["Summary"] = methodSyntax != null ? GetParamSummary(methodSyntax, parameterSymbol.Name) : string.Empty
        };
    }

    #region XML 注释处理工具

    private string GetNodeSummary(SyntaxNode node)
    {
        var xmlTrivia = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        if (xmlTrivia == default) return string.Empty;

        var xmlStructure = xmlTrivia.GetStructure() as DocumentationCommentTriviaSyntax;
        var summaryElement = xmlStructure?.Content.OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString().Equals("summary", StringComparison.OrdinalIgnoreCase));

        return summaryElement != null ? CleanXmlText(summaryElement.Content.ToString()) : string.Empty;
    }

    private string GetParamSummary(MethodDeclarationSyntax methodSyntax, string paramName)
    {
        var xmlTrivia = methodSyntax.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var xmlStructure = xmlTrivia.GetStructure() as DocumentationCommentTriviaSyntax;

        var paramElement = xmlStructure?.Content.OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString().Equals("param", StringComparison.OrdinalIgnoreCase) &&
                                 e.StartTag.Attributes.Any(a => a.ToString().Contains($"\"{paramName}\"")));

        return paramElement != null ? CleanXmlText(paramElement.Content.ToString()) : string.Empty;
    }

    private string CleanXmlText(string text) => text.Replace("///", "").Trim();

    #endregion

    private static bool IsRecordType(TypeDeclarationSyntax syntax, out bool isRecordStruct)
    {
        isRecordStruct = false;
        var hasRecordKeyword = syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.RecordKeyword));
        if (!hasRecordKeyword) return false;
        isRecordStruct = syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.StructKeyword));
        return true;
    }

    private async Task<Compilation> CreateCompilationAsync(string projectPath, CancellationToken ct)
    {
        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, null, ct);
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation == null) throw new Exception($"无法解析项目编译对象: {projectPath}");
        return compilation;
    }
}