using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core;

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

        /// <summary>
        /// 初始化源生成器，设置增量生成管道
        /// </summary>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            LogDebug("⏱️ 初始化 CodeMetaDataExtractor 生成器");

            // 1. 筛选带有有 [GenerateCode] 特性的类声明
            var candidateClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => IsCandidateClass(node),
                    transform: (ctx, _) => ExtractGenerationInfo(ctx)
                )
                .Where(info => info != null);

            LogDebug("✅ 已创建类筛选数据流");

            // 2. 注册代码生成输出（修改部分）
            context.RegisterSourceOutput(candidateClasses.Collect(), (spc, classInfos) =>
            {
                LogDebug(spc, $"⚛️ 开始处理 {classInfos.Length} 个类的代码生成");

                // 收集所有元数据
                var allMetadatas = new List<ClassMetadata>();

                foreach (var info in classInfos)
                {
                    try
                    {
                        GenerateMetaFile(spc, info.Metadata);
                        allMetadatas.Add(info.Metadata);
                        LogDebug(spc, $"🔅 已生成 {info.Metadata.ClassName} 的元数据文件");
                    }
                    catch (Exception ex)
                    {
                        LogDebug(spc, $"⚠️ 生成 {info.Metadata.ClassName} 失败: {ex.Message}");
                        ReportError(spc, $"生成 {info.Metadata.ClassName} 时出错: {ex.Message}");
                    }
                }

                // 生成元数据收集器
                GenerateMetaCollector(spc, allMetadatas);

                GenerateDebugLogFile(spc);
                LogDebug(spc, "💯 代码生成流程完成");
            });

            LogDebug("✅ 初始化完成，等待生成触发");
        }

        /// <summary>
        /// 从语法上下文提取生成所需信息
        /// </summary>
        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            if (!(context.SemanticModel.GetDeclaredSymbol(classDecl) is INamedTypeSymbol classSymbol))
            {
                LogDebug($"⚠️ 无法获取类符号: {classDecl.Identifier.Text}");
                return null;
            }

            // 检查是否有 [GenerateCode] 特性
            if (!CodeAnalysisHelper.HasGenerateCodeAttribute(context.SemanticModel.Compilation, classDecl))
                return null;

            // 提取元数据并转换为强类型
            var rawMetadata = ConvertToRawMetadata(classDecl, context.SemanticModel, classDecl.SyntaxTree.FilePath);
            var classMetadata = ConvertToClassMetadata(rawMetadata);

            // 提取特性参数
            var generateAttribute = CodeAnalysisHelper.GetGenerateAttribute(
                context.SemanticModel.Compilation,
                classSymbol,
                GenerateCodeAttribute.TypeFullName
            );
            var (_, templateName, _) = CodeAnalysisHelper.ExtractGenerateAttributeParams(generateAttribute);

            return new ClassGenerationInfo
            {
                Metadata = classMetadata,
                GenerateMode = GetGenerateMode(classSymbol),
                TemplateName = templateName ?? DefaultTemplateName
            };
        }

        /// <summary>
        /// 从编译上下文提取元数据（无文件操作）
        /// </summary>
        public IEnumerable<RawMetadata> Extract(Compilation compilation)
        {
            var results = new List<RawMetadata>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                try
                {
                    var root = syntaxTree.GetRoot();
                    var classes = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Where(c => CodeAnalysisHelper.HasGenerateCodeAttribute(compilation, c));

                    foreach (var classDecl in classes)
                    {
                        var semanticModel = compilation.GetSemanticModel(syntaxTree);
                        var metadata = ConvertToRawMetadata(classDecl, semanticModel, syntaxTree.FilePath);
                        results.Add(metadata);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new RawMetadata
                    {
                        SourceId = syntaxTree.FilePath,
                        SourceType = "Error",
                        ExtractionLogs = { $"提取失败: {ex.Message}" }
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// 将类声明转换为原始元数据
        /// </summary>
        private RawMetadata ConvertToRawMetadata(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            string filePath)
        {
            if (!(semanticModel.GetDeclaredSymbol(classDecl) is INamedTypeSymbol classSymbol))
            {
                return new RawMetadata
                {
                    SourceId = classDecl.Identifier.Text,
                    SourceType = "Error",
                    ExtractionLogs = { "无法获取类的语义符号信息" }
                };
            }

            var logs = new List<string>
            {
                $"[{DateTime.Now:HH:mm:ss}] 提取类: {classSymbol.Name} (文件: {System.IO.Path.GetFileName(filePath)})"
            };

            // 获取生成特性参数
            var generateAttribute = CodeAnalysisHelper.GetGenerateAttribute(
                semanticModel.Compilation,
                classSymbol,
                GenerateCodeAttribute.TypeFullName
            );
            var (_, templateName, _) = CodeAnalysisHelper.ExtractGenerateAttributeParams(generateAttribute);

            // 提取基类信息
            var baseType = classSymbol.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
            // 提取接口信息
            var interfaces = classSymbol.AllInterfaces
                .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToList();

            return new RawMetadata
            {
                SourceId = classSymbol.Name,
                SourceType = "Class",
                Data = new Dictionary<string, object>
                {
                    { "Namespace", classSymbol.ContainingNamespace.ToString() },
                    { "ClassName", classSymbol.Name },
                    { "FullName", $"{classSymbol.ContainingNamespace}.{classSymbol.Name}" },
                    { "Methods", ExtractMethodMetadataList(classSymbol) },
                    { "ImplementedInterfaces", interfaces },
                    { "GenerateMode", GetGenerateMode(classSymbol) },
                    { "TemplateName", templateName ?? DefaultTemplateName },
                    { "BaseType", baseType }, // 新增基类信息
                },
                ExtractionLogs = logs
            };
        }

        /// <summary>
        /// 提取方法元数据列表（包含参数特性）
        /// </summary>
        private List<Dictionary<string, object>> ExtractMethodMetadataList(INamedTypeSymbol classSymbol)
        {
            return classSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && !IsSpecialMethod(m))
                .Select(method => new Dictionary<string, object>
                {
                    { "Name", method.Name },
                    { "ReturnType", method.ReturnType.ToDisplayString() },
                    { "IsAsync", method.IsAsync },
                    { "AccessModifier", GetAccessModifier(method.DeclaredAccessibility) },
                    { "Parameters", method.Parameters.Select(p => ExtractParameterMetadata(p)).ToList() }
                }).ToList();
        }

        /// <summary>
        /// 提取单个参数的元数据（包含特性）
        /// </summary>
        private Dictionary<string, object> ExtractParameterMetadata(IParameterSymbol parameter)
        {
            var paramData = new Dictionary<string, object>
            {
                { "Name", parameter.Name },
                { "Type", parameter.Type.ToDisplayString() },
                { "TypeFullName", parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) },
                { "IsNullable", IsNullableType(parameter.Type) },
                { "IsCollection", IsCollectionType(parameter.Type) },
                { "CollectionItemType", GetCollectionItemType(parameter.Type) },
                { "DefaultValue", parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null },
                { "Attributes", ExtractAttributeMetadataList(parameter.GetAttributes()) }
            };
            return paramData;
        }

        /// <summary>
        /// 提取特性元数据列表
        /// </summary>
        private List<Dictionary<string, object>> ExtractAttributeMetadataList(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Select(attr => new Dictionary<string, object>
            {
                { "TypeFullName", attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) },
                { "Properties", attr.NamedArguments.ToDictionary(
                    arg => arg.Key,
                    arg => arg.Value.Value ?? string.Empty)
                }
            }).ToList();
        }

        /// <summary>
        /// 将 RawMetadata 转换为强类型 ClassMetadata
        /// </summary>
        private ClassMetadata ConvertToClassMetadata(RawMetadata rawMetadata)
        {
            if (rawMetadata.Data == null)
            {
                throw new ArgumentNullException(nameof(rawMetadata.Data), "元数据为空");
            }

            return new ClassMetadata
            {
                Namespace = rawMetadata.Data["Namespace"] as string,
                ClassName = rawMetadata.Data["ClassName"] as string,
                FullName = rawMetadata.Data["FullName"] as string,
                Mode = rawMetadata.Data["GenerateMode"] as string,
                SourceType = rawMetadata.SourceType,
                TemplateName = rawMetadata.Data["TemplateName"] as string,
                Methods = ConvertToMethodMetadataList(rawMetadata.Data["Methods"] as List<Dictionary<string, object>>),
                BaseType = rawMetadata.Data["BaseType"] as string ?? string.Empty, // 映射基类信息
                ImplementedInterfaces = (rawMetadata.Data["ImplementedInterfaces"] as List<string>)?.ToList()
                                        ?? new List<string>() // 映射接口列表
            };
        }

        /// <summary>
        /// 转换方法元数据为强类型列表
        /// </summary>
        private List<MethodMetadata> ConvertToMethodMetadataList(List<Dictionary<string, object>> rawMethods)
        {
            if (rawMethods == null) return new List<MethodMetadata>();

            return rawMethods.Select(rawMethod => new MethodMetadata
            {
                Name = rawMethod["Name"] as string,
                ReturnType = rawMethod["ReturnType"] as string,
                IsAsync = (bool)rawMethod["IsAsync"],
                AccessModifier = rawMethod["AccessModifier"] as string,
                Parameters = ConvertToParameterMetadataList(rawMethod["Parameters"] as List<Dictionary<string, object>>)
            }).ToList();
        }

        /// <summary>
        /// 转换参数元数据为强类型列表（包含特性）
        /// </summary>
        private List<ParameterMetadata> ConvertToParameterMetadataList(List<Dictionary<string, object>> rawParams)
        {
            if (rawParams == null) return new List<ParameterMetadata>();

            return rawParams.Select(rawParam => new ParameterMetadata
            {
                Name = rawParam["Name"] as string,
                TypeName = rawParam["Type"] as string,
                TypeFullName = rawParam["TypeFullName"] as string,
                IsNullable = (bool)rawParam["IsNullable"],
                IsCollection = (bool)rawParam["IsCollection"],
                CollectionItemType = rawParam["CollectionItemType"] as string,
                Attributes = ConvertToAttributeMetadataList(rawParam["Attributes"] as List<Dictionary<string, object>>)
            }).ToList();
        }

        /// <summary>
        /// 转换特性元数据为强类型列表
        /// </summary>
        private List<AttributeMetadata> ConvertToAttributeMetadataList(List<Dictionary<string, object>> rawAttributes)
        {
            if (rawAttributes == null) return new List<AttributeMetadata>();

            return rawAttributes.Select(rawAttr => new AttributeMetadata
            {
                TypeFullName = rawAttr["TypeFullName"] as string,
                Properties = rawAttr["Properties"] as Dictionary<string, object> ?? new Dictionary<string, object>()
            }).ToList();
        }

        #region 辅助方法

        /// <summary>
        /// 转换访问修饰符为字符串
        /// </summary>
        private static string GetAccessModifier(Accessibility accessibility)
        {
            return CodeAnalysisHelper.GetAccessModifier(accessibility);
        }

        /// <summary>
        /// 获取集合元素类型
        /// </summary>
        private static string GetCollectionItemType(ITypeSymbol type)
        {
            return CodeAnalysisHelper.GetCollectionItemType(type);
        }

        /// <summary>
        /// 从特性获取生成模式
        /// </summary>
        private static string GetGenerateMode(INamedTypeSymbol classSymbol)
        {
            var generateAttribute = classSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName);

            if (generateAttribute == null)
                return "Full";

            var typeArg = generateAttribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == "Type");

            return typeArg.Value.Value?.ToString() ?? "Full";
        }

        /// <summary>
        /// 判断是否为候选类（类声明且包含特性）
        /// </summary>
        private static bool IsCandidateClass(SyntaxNode node)
        {
            return CodeAnalysisHelper.IsCandidateClass(node);
        }

        /// <summary>
        /// 判断是否为特殊方法（属性访问器、事件访问器等）
        /// </summary>
        private static bool IsSpecialMethod(IMethodSymbol method)
        {
            return CodeAnalysisHelper.IsSpecialMethod(method);
        }

        /// <summary>
        /// 判断是否为可空类型
        /// </summary>
        private static bool IsNullableType(ITypeSymbol type)
        {
            return CodeAnalysisHelper.IsNullableType(type);
        }

        /// <summary>
        /// 判断是否为集合类型
        /// </summary>
        private static bool IsCollectionType(ITypeSymbol type)
        {
            return CodeAnalysisHelper.IsCollectionType(type);
        }

        #endregion

        /// <summary>
        /// 生成所需的元数据信息（强类型）
        /// </summary>
        public class ClassGenerationInfo
        {
            public ClassMetadata Metadata { get; set; }
            public string GenerateMode { get; set; }
            public string TemplateName { get; set; }
        }
    }
}