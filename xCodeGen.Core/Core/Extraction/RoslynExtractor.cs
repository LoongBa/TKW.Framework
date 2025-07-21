using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction
{
    public interface IMetadataExtractor
    {
        ExtractedMetadata Extract();
    }

    public class RoslynExtractor : IMetadataExtractor
    {
        private readonly string _projectPath;

        public RoslynExtractor(string projectPath)
        {
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            if (!File.Exists(_projectPath))
                throw new FileNotFoundException("目标项目文件不存在", _projectPath);
        }

        public ExtractedMetadata Extract()
        {
            return Task.Run(async () => await ExtractAsync()).GetAwaiter().GetResult();
        }

        private async Task<ExtractedMetadata> ExtractAsync()
        {
            // 使用 MEF 加载 MSBuildWorkspace，避免直接依赖 MSBuild 命名空间
            var workspaceType = Type.GetType("Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace, Microsoft.CodeAnalysis.Workspaces.MSBuild");
            if (workspaceType == null)
                throw new InvalidOperationException("未能加载 MSBuildWorkspace 类型，请确保已安装 Microsoft.CodeAnalysis.Workspaces.MSBuild 包");
            dynamic workspace = Activator.CreateInstance(workspaceType);
            workspace.WorkspaceFailed += new EventHandler<dynamic>((sender, e) => {
                if (e.Diagnostic.Kind == 1) // WorkspaceDiagnosticKind.Error = 1
                {
                    Console.WriteLine($"MSBuild 工作区错误: {e.Diagnostic.Message}");
                }
            });
            var openProjectAsync = workspaceType.GetMethod("OpenProjectAsync");
            var projectTask = (Task)openProjectAsync.Invoke(workspace, new object[] { _projectPath });
            await projectTask.ConfigureAwait(false);
            var project = projectTask.GetType().GetProperty("Result").GetValue(projectTask);
            if (project == null)
                throw new InvalidOperationException("无法加载目标项目");
            var documents = (IEnumerable<object>)project.GetType().GetProperty("Documents").GetValue(project);
            var classes = new List<ClassMetadata>();
            foreach (var document in documents)
            {
                var supportsSyntaxTree = (bool)document.GetType().GetProperty("SupportsSyntaxTree").GetValue(document);
                if (!supportsSyntaxTree)
                    continue;
                var getSyntaxTreeAsync = document.GetType().GetMethod("GetSyntaxTreeAsync");
                var syntaxTreeTask = (Task)getSyntaxTreeAsync.Invoke(document, null);
                await syntaxTreeTask.ConfigureAwait(false);
                var syntaxTree = syntaxTreeTask.GetType().GetProperty("Result").GetValue(syntaxTreeTask) as SyntaxTree;
                if (syntaxTree == null)
                    continue;
                var getRootAsync = syntaxTree.GetType().GetMethod("GetRootAsync");
                var rootTask = (Task)getRootAsync.Invoke(syntaxTree, null);
                await rootTask.ConfigureAwait(false);
                var root = rootTask.GetType().GetProperty("Result").GetValue(rootTask) as SyntaxNode;
                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                var getSemanticModelAsync = document.GetType().GetMethod("GetSemanticModelAsync");
                var semanticModelTask = (Task)getSemanticModelAsync.Invoke(document, null);
                await semanticModelTask.ConfigureAwait(false);
                var semanticModel = semanticModelTask.GetType().GetProperty("Result").GetValue(semanticModelTask) as SemanticModel;
                foreach (var classDecl in classDeclarations)
                {
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                    if (classSymbol == null)
                        continue;
                    var hasGenerateAttr = classSymbol.GetAttributes().Any(a =>
                        a.AttributeClass?.Name == nameof(GenerateArtifactAttribute) ||
                        a.AttributeClass?.Name == $"{nameof(GenerateArtifactAttribute)}Attribute");
                    if (!hasGenerateAttr)
                        continue;
                    var methods = new List<MethodMetadata>();
                    foreach (var methodDecl in classDecl.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);
                        if (methodSymbol == null)
                            continue;
                        var attr = methodSymbol.GetAttributes().FirstOrDefault(a =>
                            a.AttributeClass?.Name == nameof(GenerateArtifactAttribute) ||
                            a.AttributeClass?.Name == $"{nameof(GenerateArtifactAttribute)}Attribute");
                        if (attr == null)
                            continue;
                        var artifactType = attr.NamedArguments.FirstOrDefault(x => x.Key == nameof(GenerateArtifactAttribute.ArtifactType)).Value.Value as string;
                        var templateName = attr.NamedArguments.FirstOrDefault(x => x.Key == nameof(GenerateArtifactAttribute.TemplateName)).Value.Value as string ?? "Default";
                        var parameters = methodSymbol.Parameters.Select(p => new ParameterMetadata
                        {
                            Name = p.Name,
                            TypeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            TypeFullName = p.Type.ToDisplayString(),
                            IsNullable = p.NullableAnnotation == NullableAnnotation.Annotated,
                            IsCollection = p.Type.AllInterfaces.Any(i => i.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<")),
                            CollectionItemType = p.Type is INamedTypeSymbol nt && nt.TypeArguments.Length > 0 ? nt.TypeArguments[0].ToDisplayString() : null,
                            Attributes = p.GetAttributes().Select(attrData => new AttributeMetadata
                            {
                                TypeFullName = attrData.AttributeClass?.ToDisplayString() ?? string.Empty,
                                Properties = attrData.NamedArguments.ToDictionary(x => x.Key, x => x.Value.Value)
                            }).ToList()
                        }).ToList();
                        methods.Add(new MethodMetadata
                        {
                            Name = methodSymbol.Name,
                            ReturnType = methodSymbol.ReturnType.ToDisplayString(),
                            Parameters = parameters,
                            GenerateArtifactAttribute = new GenerateArtifactAttribute
                            {
                                ArtifactType = artifactType,
                                TemplateName = templateName
                            }
                        });
                    }
                    classes.Add(new ClassMetadata
                    {
                        Namespace = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                        Name = classSymbol.Name,
                        Methods = methods
                    });
                }
            }
            return new ExtractedMetadata { Classes = classes };
        }
    }
}
