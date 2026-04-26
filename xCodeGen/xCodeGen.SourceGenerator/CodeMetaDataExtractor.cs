using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
                //Debugger.Launch();
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

        private void EnrichAndHashMetadatas(ref List<ClassMetadata> allMetadatas)
        {
            // 明确规则：以 Entity/View 为核心，优先建立与 Service/Controller 的关联关系，最后计算 Hash
            // 以 Entity/View 为主建立关联与计算 Hash，Hash 计算基于核心属性和关联关系，确保同一实体的不同版本能正确识别
            if (allMetadatas == null || allMetadatas.Count == 0) return;

            var filteredList = new List<ClassMetadata>();

            // --- 第一步：身份判定与 Type 归类 ---
            foreach (var m in allMetadatas)
            {
                // 0. 过滤 interface ：只有实现了 IDomainService 的接口才加入列表
                if (m.TypeKind.Equals("Interface", StringComparison.OrdinalIgnoreCase))
                {
                    // 其它接口也视为 Other，且不加入后续处理列表 filteredList
                    if (ImplementsInterface(m, "IDomainService")
                        && m.Namespace.IndexOf(".Interfaces", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        m.Type = MetaType.Interface;
                        if (filteredList.All(i => i.ClassName != m.ClassName))
                            filteredList.Add(m);    // 防止重复
                    }
                    continue;
                }
                // 1. 优先判定 View/Entity
                // 标记了 DomainGenerateCodeAttribute 特性
                var genCodeAttr = FindAttribute(m.Attributes, "DomainGenerateCodeAttribute");
                if (genCodeAttr != null)
                {
                    m.UserType = GetStringProp(genCodeAttr, "UserType");
                    var isView = GetBoolProp(genCodeAttr, "IsView", false);
                    m.Type = isView ? MetaType.View : MetaType.Entity;
                    filteredList.Add(m);
                    continue;
                }

                // 2. 判定 DataService 基于基类 DomainDataServiceBase
                if (m.BaseType != null &&
                    (m.BaseType.IndexOf("DomainDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0
                     || m.BaseType.IndexOf("DomainReadOnlyDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    m.Type = MetaType.DataService;
                    filteredList.Add(m);
                    continue;
                }

                // 3. 判定 DomainController
                // 继承自 DomainControllerBase
                // 或 实现 IAopContract（继承了 IDomainService 更广泛适配，如手写版本）
                if ((m.BaseType != null && m.BaseType.IndexOf("DomainControllerBase", StringComparison.OrdinalIgnoreCase) >= 0)
                    || ImplementsInterface(m, "IAopContract"))
                {
                    m.Type = MetaType.Controller;
                    // 提取契约接口
                    var contractInterface = m.ImplementedInterfaces.FirstOrDefault(i => i.IndexOf("IAopContract", StringComparison.OrdinalIgnoreCase) < 0);
                    if (contractInterface != null) 
                        m.InterfaceName = contractInterface;

                    filteredList.Add(m);
                    continue;
                }

                // 4. 判定 DomainService 或 Decorator
                // 实现 IDomainService 接口的其余类
                // 其中继承自 DomainControllerDecoratorBase 为 Decorator
                if (ImplementsInterface(m, "IDomainService"))
                {
                    var isDecoBase = m.BaseType != null &&
                                     m.BaseType.IndexOf("DomainControllerDecoratorBase", StringComparison.OrdinalIgnoreCase) >= 0;
                    var isService = ImplementsInterface(m, "IDomainService");
                    m.Type = isDecoBase ? MetaType.Decorator : isService ? MetaType.Service : MetaType.Other;
                    if (m.Type != MetaType.Other) filteredList.Add(m);
                    continue;
                }
                // 5. 其他类归为 Other，且不加入后续处理列表 filteredList
                m.Type = MetaType.Other;
            }

            // 经过前期处理，梳理关联关系，便于后续使用
            allMetadatas = filteredList;
            var entities = allMetadatas
                .Where(x => x.Type == MetaType.Entity || x.Type == MetaType.View);
            var interfaces = allMetadatas
                .Where(x => x.Type == MetaType.Interface)
                .ToDictionary(x => x.ClassName, x => x);
            var services = allMetadatas
                .Where(x => x.Type == MetaType.Service)
                .ToDictionary(x => x.ClassName, x => x);
            var controllers = allMetadatas
                .Where(x => x.Type == MetaType.Controller)
                .ToDictionary(x => x.ClassName, x => x);
            var decorators = allMetadatas
                .Where(x => x.Type == MetaType.Decorator)
                .ToDictionary(x => x.ClassName, x => x);

            // --- 第二步：以 Entity/View 为主建立关联与计算 Hash ---
            using var sha = SHA256.Create();
            foreach (var entity in entities)
            {
                var entityName = entity.ClassName;
                // 检查是否存在匹配的 Service/Controller，存入 Settings 供模板使用
                var hasService = services.TryGetValue($"{entityName}Service", out var service);
                var hasInterface = interfaces.TryGetValue($"I{entityName}Service", out var @interface);
                var hasController = controllers.TryGetValue($"{entityName}Service", out var controller);
                if (!hasController)
                    hasController = controllers.TryGetValue($"{entityName}Controller", out controller);
                // 关联服务和控制器
                if (hasController)
                {
                    entity.Controller = controller;
                    controller.Entity = entity;
                    // 匹配接口：优先匹配 Controller 的接口，否则匹配 Service 的接口
                    if (!hasInterface)
                        hasInterface = interfaces.TryGetValue($"I{controller.ClassName}", out @interface);
                    if (ImplementsInterface(controller, $"I{controller.ClassName}")
                        && hasInterface)
                        entity.Interface = controller.Interface = @interface;
                    // 如果装饰器已经存在
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
                    // 匹配接口：仅匹配 Service 的接口
                    if (ImplementsInterface(service, $"I{service.ClassName}")
                        && hasInterface)
                        entity.Interface = service.Interface = @interface;
                }
                // 修正可能错误的命名空间（如果接口存在且命名空间不正确）
                entity.Interface?.Namespace = entity.Namespace.Replace(".Entities", ".Interfaces");
                entity.Interface?.UserType = entity.UserType;
                entity.Service?.UserType = entity.UserType;
                entity.Controller?.UserType = entity.UserType;

                // 构造 Hash 源字符串
                var sb = new StringBuilder();
                sb.Append(entity.Namespace).Append("|")
                    .Append(entityName).Append("|")
                    .Append(entity.Type).Append("|") // 使用 Type 代替多个布尔值
                    .Append(hasService).Append("|")
                    .Append(hasController).Append("|")
                    .Append(entity.HasDecoratorCandidate).Append("|")
                    .Append(entity.DecoratorTypeFullName);

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                entity.SourceHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}