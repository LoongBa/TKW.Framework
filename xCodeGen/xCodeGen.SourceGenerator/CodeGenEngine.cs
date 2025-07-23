using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.SourceGenerator
{
    //[Generator]
    public class CodeGenEngine : IIncrementalGenerator
    {
        // 保留无参构造函数（即使为空）
        public CodeGenEngine()
        {
        }
        //private RazorLightEngine _razorEngine;
        private static readonly HashSet<string> GeneratedSources = new HashSet<string>();

        private const string _Log_Initialize_Filename = "CodeGen_Initialize_Log.g.cs";
        private const string _Log_Execute_Filename = "CodeGen_Execute_Log.g.cs";
        private const string _Log_CallExecute_Filename = "CodeGen_CallExecute_Log.g.cs";

        private void debugOut(IncrementalGeneratorInitializationContext context, string logFilename, List<string> logs)
        {
            var sb = new StringBuilder();
            foreach (var log in logs)
                sb.AppendLine($"// {log}");
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource(logFilename, SourceText.From(sb.ToString(), Encoding.UTF8));
            });
        }

        private void debugOut(SourceProductionContext context, string logFilename, List<string> logs)
        {
            var sb = new StringBuilder();
            foreach (var log in logs)
                sb.AppendLine($"// {log}");
            context.AddSource($"{logFilename}", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
        private void debugOut(SourceProductionContext context, string logFilename, string log)
        {
            context.AddSource($"{logFilename}", SourceText.From(log, Encoding.UTF8));
        }

        new List<string> _logMessages = new List<string> { "Initialize() 生成器已被触发" };
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 延迟 10 秒，用于手动附加调试器
            //System.Threading.Thread.Sleep(20000);
            // 1. 扫描所有被标记的类型
            var typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => IsCandidateType(s),
                    transform: (ctx, _) => GetTypeSymbol(ctx))
                .Where(m => m != null);

            _logMessages.Add("\t 已获取类型声明数据流");

            // 2. 处理额外文件（配置和模板）
            var additionalFiles = context.AdditionalTextsProvider
                .Collect()
                .Select((files, _) => new AdditionalFilesData
                {
                    Config = GetConfigFile(files),
                    Templates = GetTemplateFiles(files)
                });

            _logMessages.Add("\t 已获取额外文件数据流");

            // 3. 获取编译上下文数据流
            var compilationProvider = context.CompilationProvider;
            _logMessages.Add("\t 已获取编译上下文数据流");

            // 4. 合并数据流：(类型, 额外文件) → 再与编译上下文合并
            var pipeline = typeDeclarations
                .Combine(additionalFiles)          // 第一步合并：(TTypeSymbol, AdditionalFilesData)
                .Combine(compilationProvider)      // 第二步合并：((TTypeSymbol, AdditionalFilesData), Compilation)
                .Select((data, _) => new          // 转换为便于使用的对象
                {
                    TypeSymbol = data.Left.Left,
                    FilesData = data.Left.Right,
                    Compilation = data.Right
                });

            try
            {
                // 5. 生成代码（此时可访问Compilation）
                _logMessages.Add("\t 开始注册代码生成输出");
                context.RegisterSourceOutput(pipeline, (spc, source) =>
                {
                    debugOut(spc, _Log_CallExecute_Filename, "// 生成准备");
                    // 验证所有必要数据是否存在
                    if (source.TypeSymbol == null || source.FilesData == null || source.Compilation == null)
                    {
                        debugOut(spc, _Log_CallExecute_Filename, "// 跳过生成：存在空数据");
                        return;
                    }

                    // 传入Compilation到Execute方法
                    Execute(
                        spc,
                        source.TypeSymbol,
                        source.FilesData.Config,
                        source.FilesData.Templates,
                        source.Compilation
                    );
                    debugOut(spc, _Log_CallExecute_Filename, $"// 生成完成：{source.TypeSymbol.Name}");
                });
                _logMessages.Add("\t 代码生成输出注册完成");
            }
            catch (Exception e)
            {
                _logMessages.Add($"\t 初始化异常：{e.Message}");
            }

            _logMessages.Add("Initialize() 执行完毕");
            debugOut(context, _Log_Initialize_Filename, _logMessages);
        }

        private void Execute(SourceProductionContext context, INamedTypeSymbol typeSymbol, 
            CodeGenConfig config, Dictionary<string, string> templates,
            // 新增：编译上下文参数
            Compilation compilation )
        {
            // 输出调试信息
            var logs = new List<string> { "Execute() 已被触发" };
            debugOut(context, _Log_Execute_Filename, logs);
            try
            {
                logs.Add("查找：" + typeSymbol.Name + " 特性：" + GenerateArtifactAttribute.TypeFullName);

                // 获取 GenerateArtifactAttribute 类型符号
                var fullName = GenerateArtifactAttribute.TypeFullName;
                var generateAttributeType = compilation.GetTypeByMetadataName(fullName);
                logs.Add(generateAttributeType == null
                    ? fullName + " generateAttributeType NULL!"
                    : fullName + " generateAttributeType OK.");

                // 查找特性
                var generateAttribute = typeSymbol.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttributeType));

                //logs.Add("SymbolEqualityComparer");
                if (generateAttribute == null)
                {
                    return;
                }

                // 提取元数据
                var metadata = ExtractMetadata(typeSymbol);

                // 获取特性参数
                var artifactType = GetAttributeValue(generateAttribute, nameof(GenerateArtifactAttribute.ArtifactType));
                var templateName = GetAttributeValue(generateAttribute, nameof(GenerateArtifactAttribute.TemplateName)) ?? "Default";
                var overwrite = GetAttributeBoolValue(generateAttribute, nameof(GenerateArtifactAttribute.Overwrite)) ?? false;

                if (string.IsNullOrEmpty(artifactType))
                {
                    ReportError(context, $"类型 {GetTypeName(typeSymbol)} 的 ArtifactType 未设置，跳过处理");
                    return;
                }

                // 生成代码
                GenerateCodeForType(context, metadata, artifactType, templateName, overwrite, config, templates);
            }
            catch (Exception ex)
            {
                logs.Add($"代码生成过程中发生错误: {ex.Message}");
                debugOut(context, _Log_Execute_Filename, logs);
                ReportError(context, $"代码生成过程中发生错误: {ex.Message}\n{ex.StackTrace}");
            }
            // 输出调试信息到日志文件
            debugOut(context, _Log_Execute_Filename, logs);
        }

        private static bool IsCandidateType(SyntaxNode node)
        {
            //return node is TypeDeclarationSyntax typeDeclaration &&
            return node is ClassDeclarationSyntax typeDeclaration &&
                   typeDeclaration.AttributeLists.Any();
        }

        private static INamedTypeSymbol GetTypeSymbol(GeneratorSyntaxContext context)
        {
            var typeDeclaration = (ClassDeclarationSyntax)context.Node;
            //var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            return context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        }

        private static ClassMetadata ExtractMetadata(INamedTypeSymbol typeSymbol)
        {
            return new ClassMetadata
            {
                Namespace = typeSymbol.ContainingNamespace.ToString(),
                Name = typeSymbol.Name,
                FullName = GetTypeName(typeSymbol), // 替代FullName属性
                Methods = typeSymbol.GetMembers().OfType<IMethodSymbol>()
                    .Where(method => !method.IsImplicitlyDeclared)
                    .Select(method => new MethodMetadata
                    {
                        Name = method.Name,
                        ReturnType = method.ReturnType.ToString(),
                        IsAsync = method.IsAsync,
                        Parameters = method.Parameters.Select(p => new ParameterMetadata
                        {
                            Name = p.Name,
                            TypeName = p.Type.Name,
                            TypeFullName = p.Type.ToString(),
                            IsNullable = IsNullableType(p.Type),
                            IsCollection = IsCollectionType(p.Type),
                            CollectionItemType = GetCollectionItemType(p.Type)
                        }).ToList()
                    }).ToList()
            };
        }

        // 获取类型全名（替代FullName属性）
        private static string GetTypeName(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
                return typeSymbol.Name;

            return $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
        }

        // 获取特性字符串值
        private static string GetAttributeValue(AttributeData attribute, string propertyName)
        {
            if (attribute.ConstructorArguments.Length > 0)
            {
                return attribute.ConstructorArguments[0].Value?.ToString();
            }

            var namedArg = attribute.NamedArguments.FirstOrDefault(a =>
                a.Key == propertyName);

            return namedArg.Value.Value?.ToString();
        }

        // 获取特性布尔值
        private static bool? GetAttributeBoolValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a =>
                a.Key == propertyName);

            return namedArg.Value.Value as bool?;
        }

        private static bool IsNullableType(ITypeSymbol type)
        {
            // 检查可空值类型 (T?)
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return true;
            }

            // 检查可为空引用类型
            if (type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return true;
            }

            return false;
        }

        private static bool IsCollectionType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
                return true;

            var namedType = type as INamedTypeSymbol;
            if (namedType == null)
                return false;

            return namedType.AllInterfaces.Any(i =>
                i.ToString() == "System.Collections.Generic.IEnumerable`1");
        }

        private static string GetCollectionItemType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arrayType)
                return arrayType.ElementType.Name;

            var namedType = type as INamedTypeSymbol;
            if (namedType?.TypeArguments.Length > 0)
                return namedType.TypeArguments[0].Name;

            return null;
        }

        private void GenerateCodeForType(SourceProductionContext context,
            ClassMetadata metadata, string artifactType, string templateName,
            bool overwrite, CodeGenConfig config, Dictionary<string, string> templates)
        {
            try
            {
                // 获取命名规则
                var namingRule = config.NamingRules?.FirstOrDefault(r =>
                    string.Equals(r.ArtifactType, artifactType, StringComparison.OrdinalIgnoreCase))
                    ?? new NamingRule { ArtifactType = artifactType, Pattern = "{Name}{ArtifactType}" };

                // 生成输出文件名
                var outputName = namingRule.Pattern
                    .Replace("{Name}", metadata.Name)
                    .Replace("{ArtifactType}", artifactType);

                // 检查是否需要覆盖
                if (!overwrite && GeneratedSources.Contains(outputName))
                {
                    return;
                }

                // 渲染模板
                string generatedCode;
                // 获取模板内容（从附加文件中获取，避免文件系统操作）
                var templateKey = $"{artifactType}/{templateName}";
                if (!templates.TryGetValue(templateKey, out var templateContent))
                {
                    // 尝试其他可能的模板名
                    var alternativeKey1 = $"{templateName}.cshtml";
                    var alternativeKey2 = $"{artifactType}Template.cshtml";

                    if (templates.TryGetValue(alternativeKey1, out var altContent1))
                    {
                        templateContent = altContent1;
                    }
                    else if (templates.TryGetValue(alternativeKey2, out var altContent2))
                    {
                        templateContent = altContent2;
                    }
                    else
                    {
                        generatedCode = $"// 模板未找到: {templateKey}, {alternativeKey1}, {alternativeKey2}";
                        context.AddSource($"{outputName}.cs", generatedCode);
                        return;
                    }
                }

                // 准备模板数据
                var templateData = new
                {
                    Metadata = metadata,
                    ArtifactType = artifactType,
                    Config = config
                };

                try
                {
                    // 使用RazorLight渲染模板（使用内存缓存）
                    //generatedCode = _razorEngine.CompileRenderStringAsync(outputName, templateContent, templateData).Result;
                    //TODO: 暂时不使用 RazorLight
                    generatedCode = $"// 调试信息：类型 {metadata.Name}, ArtifactType: {artifactType}, TemplateName: {templateName}";
                    context.AddSource($"{outputName}.cs", generatedCode);
                }
                catch (Exception ex)
                {
                    generatedCode = $"// 模板渲染失败: {ex.Message}\n// 模板: {templateKey}";
                }

                // 添加生成的代码
                context.AddSource($"{outputName}.cs", generatedCode);
                GeneratedSources.Add(outputName);
            }
            catch (Exception ex)
            {
                ReportError(context, $"为类型 {metadata.Name} 生成代码时发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 从附加文件中获取配置
        /// </summary>
        private static CodeGenConfig GetConfigFile(IEnumerable<AdditionalText> files)
        {
            var configFile = files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f.Path), "CodeGen.config.json", StringComparison.OrdinalIgnoreCase));

            if (configFile == null)
            {
                // 可选：记录警告（需通过上下文传递诊断）
                return new CodeGenConfig();
            }

            try
            {
                var content = configFile.GetText()?.ToString();
                if (string.IsNullOrEmpty(content))
                {
                    return new CodeGenConfig();
                }
                return CodeGenConfig.FromJson(content);
            }
            catch (Exception ex)
            {
                // 配置解析失败时返回默认配置，避免生成器崩溃
                // 可通过上下文报告错误（需调整方法参数传入context）
                return new CodeGenConfig();
            }
        }

        // 从附加文件中获取所有模板
        private static Dictionary<string, string> GetTemplateFiles(IEnumerable<AdditionalText> files)
        {
            var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file.Path);
                if (fileName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    var templateKey = Path.GetFileNameWithoutExtension(fileName);
                    var content = file.GetText()?.ToString() ?? string.Empty;

                    // 添加多个可能的键用于查找
                    templates[templateKey] = content;

                    var directory = Path.GetDirectoryName(file.Path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        var dirName = new DirectoryInfo(directory).Name;
                        templates[$"{dirName}/{templateKey}"] = content;
                    }
                }
            }

            return templates;
        }

        private class AdditionalFilesData
        {
            public CodeGenConfig Config { get; set; }
            public Dictionary<string, string> Templates { get; set; } = new Dictionary<string, string>();
        }

        private static void ReportError(SourceProductionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "XCG001", "代码生成错误",
                    "代码生成过程中发生错误: {0}", "xCodeGen",
                    DiagnosticSeverity.Error, true),
                Location.None, message));
        }
    }
}