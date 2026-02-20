using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction;

/// <summary>
/// 基于 Roslyn 语义分析的元数据提取器实现
/// </summary>
public class RoslynExtractor : IMetaDataExtractor
{
    // 明确对应代码来源类型
    public MetadataSource SourceType => MetadataSource.Code;

    /// <summary>
    /// 提取元数据，支持 CancellationToken
    /// </summary>
    public async Task<IEnumerable<RawMetadata>> ExtractAsync(
        ExtractorOptions options,
        CancellationToken cancellationToken = default)
    {
        var rawMetadataList = new List<RawMetadata>();

        // 模拟加载编译环境（实际环境应使用 MSBuildWorkspace 加载 .csproj）
        var compilation = await CreateCompilationAsync(options.ProjectPath, cancellationToken);

        foreach (var tree in compilation.SyntaxTrees)
        {
            // 支持取消操作
            cancellationToken.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(cancellationToken);

            // 查找所有标记了 [GenerateArtifact] 的类
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                // 只有包含特定特性的类才进行提取
                if (!symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "GenerateArtifactAttribute"))
                    continue;

                rawMetadataList.Add(new RawMetadata
                {
                    SourceType = this.SourceType.ToString(),
                    SourceId = symbol.ToDisplayString(),
                    Data = ExtractClassData(symbol, classDecl, semanticModel)
                });
            }
        }

        return rawMetadataList;
    }

    private Dictionary<string, object> ExtractClassData(INamedTypeSymbol symbol, 
        ClassDeclarationSyntax syntax, SemanticModel model)
    {
        return new Dictionary<string, object>
        {
            ["Namespace"] = symbol.ContainingNamespace.ToDisplayString(),
            ["ClassName"] = symbol.Name,
            ["Summary"] = GetNodeSummary(syntax), // 提取类注释
            ["Methods"] = symbol.GetMembers().OfType<IMethodSymbol>()
                .Select(ExtractMethodData).ToList()
        };
    }

    private Dictionary<string, object> ExtractMethodData(IMethodSymbol methodSymbol)
    {
        var methodSyntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;

        return new Dictionary<string, object>
        {
            ["MethodName"] = methodSymbol.Name,
            ["ReturnType"] = methodSymbol.ReturnType.ToDisplayString(),
            ["Summary"] = methodSyntax != null ? GetNodeSummary(methodSyntax) : string.Empty, // 提取方法注释
            ["Parameters"] = methodSymbol.Parameters.Select(p => new Dictionary<string, object>
            {
                ["ParameterName"] = p.Name,
                ["Type"] = p.Type.ToDisplayString(),
                ["Summary"] = methodSyntax != null ? GetParamSummary(methodSyntax, p.Name) : string.Empty // 提取参数注释
            }).ToList()
        };
    }

    #region XML 注释辅助方法

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

    private async Task<Compilation> CreateCompilationAsync(string projectPath, CancellationToken ct)
    {
        // 此处应集成 MSBuildWorkspace 逻辑，暂用模拟实现
        await Task.Delay(10, ct);
        return CSharpCompilation.Create("TemporaryCompilation");
    }
}