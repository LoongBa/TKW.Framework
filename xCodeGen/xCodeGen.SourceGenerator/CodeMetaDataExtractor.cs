using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.SourceGenerator
{
    [Generator]
    public partial class CodeMetaDataExtractor : IMetaDataExtractor, IIncrementalGenerator
    {
        public MetadataSource SourceType => MetadataSource.Code;

        // 核心配置：使用简短名称格式 (利用源文件的 usings 自动简化类型，如 System.Threading.Tasks.Task 简化为 Task)
        private static readonly SymbolDisplayFormat ShortTypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

        public Task<IEnumerable<RawMetadata>> ExtractAsync(ExtractorOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidateTypes = context.SyntaxProvider.CreateSyntaxProvider((node, _) => CodeAnalysisDiagnostics.IsCandidateClass(node), (ctx, _) => ExtractGenerationInfo(ctx)).Where(info => info != null);
            var projectInfoProvider = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider).Select((pair, _) => new ProjectInfo(pair.Right, pair.Left));

            context.RegisterSourceOutput(candidateTypes.Collect().Combine(projectInfoProvider), (spc, combined) =>
            {
                var (infos, projectInfo) = combined;
                var groupedInfos = infos.GroupBy(i => (i.Metadata.FullName ?? $"{i.Metadata.Namespace}.{i.Metadata.ClassName}"), StringComparer.Ordinal).Select(g => g.First()).ToList();
                var allMetadatas = groupedInfos.Select(i => i.Metadata).ToList();
                EnrichAndHashMetadatas(ref allMetadatas);

                foreach (var entity in allMetadatas.Where(m => m.Type == MetaType.Entity || m.Type == MetaType.View)) GenerateMetaFile(spc, entity);
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
            var md = new ClassMetadata
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
                Attributes = ConvertToAttributeMetadataList(GetRawList(data, "Attributes")),
                Methods = ConvertToMethodMetadataList(GetRawList(data, "Methods")),
                Properties = ConvertToPropertyMetadataList(GetRawList(data, "Properties"))
            };

            // 获取存放好的 Usings 并存储在 GenerateCodeSettings 备用
            if (data.TryGetValue("Usings", out var u) && u is List<string> usings)
            {
                md.GenerateCodeSettings["Usings"] = usings;
            }
            return md;
        }

        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods) =>
            rawMethods?.Select(m => new MethodMetadata
            {
                Name = m["Name"] as string,
                ReturnType = m["ReturnType"] as string,
                IsAsync = (bool)m["IsAsync"],
                Summary = m.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                AccessModifier = m["AccessModifier"] as string,
                Parameters = ConvertToParameterMetadataList(GetRawList(m, "Parameters")),
                Attributes = ConvertToAttributeMetadataList(GetRawList(m, "Attributes"))
            }).ToList() ?? new List<MethodMetadata>();

        private List<PropertyMetadata> ConvertToPropertyMetadataList(List<Dictionary<string, object>> rawProps) =>
            rawProps?.Select(p => new PropertyMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                TypeFullName = p["TypeFullName"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                Attributes = ConvertToAttributeMetadataList(GetRawList(p, "Attributes"))
            }).ToList() ?? new List<PropertyMetadata>();

        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams) =>
            rawParams?.Select(p => new ParameterMetadata
            {
                Name = p["Name"] as string,
                TypeName = p["Type"] as string,
                IsNullable = (bool)p["IsNullable"],
                Summary = p.TryGetValue("Summary", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                Attributes = ConvertToAttributeMetadataList(GetRawList(p, "Attributes"))
            }).ToList() ?? new List<ParameterMetadata>();

        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttrs) =>
            rawAttrs?.Select(a => new AttributeMetadata
            {
                Name = a["Name"] as string,
                TypeFullName = a["TypeFullName"] as string,
                Properties = a.TryGetValue("Properties", out var p) ? (Dictionary<string, object>)p : new Dictionary<string, object>(),
                ConstructorArguments = a.TryGetValue("ConstructorArguments", out var c) ? (List<object>)c : new List<object>()
            }).ToList() ?? new List<AttributeMetadata>();

        private List<Dictionary<string, object>> GetRawList(Dictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var val) && val != null)
            {
                if (val is List<Dictionary<string, object>> list) return list;
                if (val is IEnumerable<Dictionary<string, object>> enumList) return enumList.ToList();
            }
            return new List<Dictionary<string, object>>();
        }

        private (string Text, string Source) GetNodeSummary(ISymbol symbol, SyntaxNode node)
        {
            var xml = symbol.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                var start = xml.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
                var end = xml.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
                if (start != -1 && end != -1) return (xml.Substring(start + 9, end - start - 9).Trim(), "Semantic XML");
            }
            var documentation = node.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)).Select(t => t.GetStructure() as DocumentationCommentTriviaSyntax).FirstOrDefault();
            if (documentation != null)
            {
                var summaryElement = documentation.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString().Equals("summary", StringComparison.OrdinalIgnoreCase));
                if (summaryElement != null) return (CleanXmlText(summaryElement.Content.ToString()), "Syntax Trivia");
            }
            return (string.Empty, "None");
        }

        private RawMetadata ConvertToRawMetadata(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return new RawMetadata { SourceType = MetadataSource.Error };
            var (summary, source) = GetNodeSummary(symbol, typeDecl);

            // 获取文件顶部的 using 指令以备用
            var usings = typeDecl.SyntaxTree.GetCompilationUnitRoot().Usings.Select(u => u.ToString()).ToList();

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
                    { "Attributes", ExtractAttributeMetadataList(symbol.GetAttributes()) },
                    { "Usings", usings }
                }
            };
        }

        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return null;

            var hasGenerateAttr = symbol.HasGenerateCodeAttribute();
            bool isController = symbol.BaseType != null && symbol.BaseType.ToDisplayString().IndexOf("DomainControllerBase", StringComparison.Ordinal) >= 0;
            var implementsDomainService = symbol.AllInterfaces.Any(i => i.ToDisplayString().IndexOf("IDomainService", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!hasGenerateAttr && !isController && !implementsDomainService) return null;

            var rawMetadata = ConvertToRawMetadata(typeDecl, context.SemanticModel);
            var classMetadata = ConvertToClassMetadata(rawMetadata);

            if (hasGenerateAttr) classMetadata.Type = MetaType.Entity;
            else if (isController) classMetadata.Type = MetaType.Controller;
            else if (implementsDomainService) classMetadata.Type = symbol.Name.EndsWith("DataService") ? MetaType.DataService : MetaType.Service;

            return new ClassGenerationInfo
            {
                Metadata = classMetadata,
                GenerateMode = GetGenerateMode(symbol),
                TemplateName = DefaultTemplateName,
                SemanticModel = context.SemanticModel
            };
        }

        private List<Dictionary<string, object>> ExtractAttributeMetadataList(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Select(attr =>
            {
                var typeFullName = attr.AttributeClass?.ToDisplayString();
                var name = attr.AttributeClass?.Name ?? string.Empty;
                var props = GetAttributeDefaults(typeFullName);

                foreach (var arg in attr.NamedArguments) props[arg.Key] = arg.Value.Value ?? string.Empty;

                // 核心拦截标记判定增强：
                bool isFilter = false;
                var baseType = attr.AttributeClass;
                while (baseType != null)
                {
                    if (baseType.Name.Contains("Filter") || baseType.Name.Contains("Interceptor"))
                    {
                        isFilter = true; break;
                    }
                    baseType = baseType.BaseType;
                }
                // 兜底策略：如果外部程序集未加载全导致基类推导断档，强行检查特性名自身是否带 Filter
                if (!isFilter && typeFullName != null && (typeFullName.Contains("Filter") || typeFullName.Contains("Interceptor")))
                {
                    isFilter = true;
                }
                props["IsDomainFilter"] = isFilter;

                var constructorArgs = attr.ConstructorArguments.Select(a => a.Value ?? string.Empty).ToList();
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
            return symbol.GetMembers().OfType<IPropertySymbol>().Select(prop =>
            {
                var syntax = prop.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                var (sum, _) = syntax != null ? GetNodeSummary(prop, syntax) : (string.Empty, "None");
                return new Dictionary<string, object>
                {
                    { "Name", prop.Name },
                    { "Type", prop.Type.ToDisplayString(ShortTypeFormat) }, // 短名称
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
                        { "ReturnType", method.ReturnType.ToDisplayString(ShortTypeFormat) }, // 短名称
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
                { "Type", parameter.Type.ToDisplayString(ShortTypeFormat) }, // 短名称
                { "IsNullable", CodeAnalysisDiagnostics.IsNullable(parameter.Type) },
                { "Summary", methodSyntax != null ? GetParamSummary(methodSyntax, parameter.Name) : string.Empty },
                { "Attributes", ExtractAttributeMetadataList(parameter.GetAttributes()) }
            };
        }

        private void EnrichAndHashMetadatas(ref List<ClassMetadata> allMetadatas)
        {
            if (allMetadatas == null || allMetadatas.Count == 0) return;
            var filteredList = new List<ClassMetadata>();

            foreach (var m in allMetadatas)
            {
                if (m.TypeKind.Equals("Interface", StringComparison.OrdinalIgnoreCase))
                {
                    if (ImplementsInterface(m, "IDomainService") && m.Namespace.IndexOf(".Interfaces", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        m.Type = MetaType.Interface;
                        if (filteredList.All(i => i.ClassName != m.ClassName)) filteredList.Add(m);
                    }
                    continue;
                }

                var genCodeAttr = FindAttribute(m.Attributes, "DomainGenerateCodeAttribute");
                if (genCodeAttr != null)
                {
                    m.UserType = TrimNamespace(GetStringProp(genCodeAttr, "UserType"));
                    m.Type = GetBoolProp(genCodeAttr, "IsView", false) ? MetaType.View : MetaType.Entity;
                    filteredList.Add(m);
                    continue;
                }

                if (m.BaseType != null && (m.BaseType.IndexOf("DomainDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0 || m.BaseType.IndexOf("DomainReadOnlyDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    m.Type = MetaType.DataService;
                    filteredList.Add(m);
                    continue;
                }

                if (m.BaseType != null && m.BaseType.IndexOf("DomainControllerBase", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m.Type = MetaType.Controller;
                    var argsCount = m.BaseType.Split(',').Length;
                    var userTypeStr = GetGenericArgument(m.BaseType, argsCount > 1 ? 1 : 0);
                    m.UserType = TrimNamespace(userTypeStr ?? "object");
                    filteredList.Add(m);
                    continue;
                }

                if (ImplementsInterface(m, "IDomainService"))
                {
                    var isDecoBase = m.BaseType != null && m.BaseType.IndexOf("DomainControllerDecoratorBase", StringComparison.OrdinalIgnoreCase) >= 0;
                    m.Type = isDecoBase ? MetaType.Decorator : MetaType.Service;
                    if (m.Type != MetaType.Other) filteredList.Add(m);
                    continue;
                }

                m.Type = MetaType.Other;
            }

            allMetadatas = filteredList;
            var entities = allMetadatas.Where(x => x.Type == MetaType.Entity || x.Type == MetaType.View);
            var interfaces = allMetadatas.Where(x => x.Type == MetaType.Interface).ToDictionary(x => x.ClassName, x => x);
            var services = allMetadatas.Where(x => x.Type == MetaType.Service).ToDictionary(x => x.ClassName, x => x);
            var controllers = allMetadatas.Where(x => x.Type == MetaType.Controller).ToDictionary(x => x.ClassName, x => x);
            var decorators = allMetadatas.Where(x => x.Type == MetaType.Decorator).ToDictionary(x => x.ClassName, x => x);

            using var sha = SHA256.Create();
            foreach (var entity in entities)
            {
                var entityName = entity.ClassName;
                var hasService = services.TryGetValue($"{entityName}Service", out var service);
                var hasInterface = interfaces.TryGetValue($"I{entityName}Service", out var @interface);
                var hasController = controllers.TryGetValue($"{entityName}Service", out var controller);
                if (!hasController) hasController = controllers.TryGetValue($"{entityName}Controller", out controller);

                if (hasController)
                {
                    entity.Controller = controller;
                    controller.Entity = entity;
                    if (!hasInterface) hasInterface = interfaces.TryGetValue($"I{controller.ClassName}", out @interface);
                    if (ImplementsInterface(controller, $"I{controller.ClassName}") && hasInterface) entity.Interface = controller.Interface = @interface;

                    var expectedDecoName = $"{controller.ClassName}Decorator";
                    if (decorators.TryGetValue(expectedDecoName, out var decorator))
                    {
                        entity.HasDecoratorCandidate = controller.HasDecoratorCandidate = true;
                        entity.DecoratorTypeFullName = controller.DecoratorTypeFullName = decorator.FullName;
                    }
                }
                else if (hasService)
                {
                    entity.Service = service;
                    service.Entity = entity;
                    if (ImplementsInterface(service, $"I{service.ClassName}") && hasInterface) entity.Interface = service.Interface = @interface;
                }

                if (entity.Interface != null)
                {
                    entity.Interface.Namespace = entity.Namespace.Replace(".Entities", ".Interfaces");
                    entity.Interface.UserType = entity.UserType;
                }
                if (entity.Service != null) entity.Service.UserType = entity.UserType;
                if (entity.Controller != null) entity.Controller.UserType = entity.UserType;

                var sb = new StringBuilder();
                sb.Append(entity.Namespace).Append("|").Append(entityName).Append("|").Append(entity.Type).Append("|")
                  .Append(hasService).Append("|").Append(hasController).Append("|").Append(entity.HasDecoratorCandidate).Append("|").Append(entity.DecoratorTypeFullName);

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                entity.SourceHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}