using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.SourceGenerator
{
    [Generator]
    public class MetaDataExtractor : IIncrementalGenerator
    {
        public MetaDataExtractor()
        {
        }

        private static readonly string GenerateArtifactAttributeFullName = GenerateArtifactAttribute.TypeFullName;
        private readonly List<string> _debugLogs = new List<string>();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            LogDebug("⏱️ 开始初始化代码生成器");
            // 1. 筛选带有特性的类声明
            var candidateClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => IsCandidateClass(node),
                    transform: (ctx, _) => GetClassSymbolWithDebug(ctx)
                )
                .Where(symbol => symbol != null);

            LogDebug("✅ 已创建类筛选数据流");

            // 2. 收集所有符合条件的类
            var allClasses = candidateClasses.Collect();

            // 3. 结合编译上下文
            var compilationWithClasses = context.CompilationProvider
                .Combine(allClasses);

            LogDebug("✅ 已组合编译上下文和类数据");

            // 4. 注册代码生成输出
            context.RegisterSourceOutput(compilationWithClasses, (spc, data) =>
            {
                LogDebug(spc, "⚛️ 进入代码生成阶段");
                try
                {
                    var compilation = data.Left;
                    var classes = data.Right;

                    LogDebug(spc, $"⏱️ 开始处理 {classes.Length} 个类");

                    // 处理每个类
                    foreach (var classSymbol in classes)
                    {
                        ProcessClass(spc, compilation, classSymbol);
                    }

                    // 生成最终调试日志文件
                    GenerateDebugLogFile(spc);
                    LogDebug(spc, "💯 代码生成过程完成");
                }
                catch (Exception ex)
                {
                    LogDebug(spc, $"⚠️ 生成过程异常: {ex.Message}\n{ex.StackTrace}");
                    ReportError(spc, $"⚠️ 生成器执行失败: {ex.Message}");
                }
            });

            LogDebug("✅ 初始化完成，等待生成触发");
        }

        private void ProcessClass(SourceProductionContext context, Compilation compilation, INamedTypeSymbol classSymbol)
        {
            try
            {
                LogDebug(context, $"⏱️ 开始处理类: {classSymbol.Name}");

                // 获取生成特性
                var generateAttribute = GetGenerateAttribute(compilation, classSymbol);
                if (generateAttribute == null)
                {
                    LogDebug(context, $"⚠️ {classSymbol.Name} 未找到 GenerateArtifactAttribute，跳过");
                    return;
                }

                // 提取类元数据
                var classMetadata = ExtractClassMetadata(classSymbol);
                LogDebug(context, $"✅ 已提取 {classSymbol.Name} 的元数据，包含 {classMetadata.Methods.Count} 个方法");

                // 获取特性参数
                var artifactType = GetAttributeValue(generateAttribute, nameof(GenerateArtifactAttribute.ArtifactType));
                var templateName = GetAttributeValue(generateAttribute, nameof(GenerateArtifactAttribute.TemplateName)) ?? "Default";
                var overwrite = GetAttributeBoolValue(generateAttribute, nameof(GenerateArtifactAttribute.Overwrite)) ?? false;

                LogDebug(context, $"☣️ 特性参数 - ArtifactType: {artifactType}, TemplateName: {templateName}, Overwrite: {overwrite}");

                if (string.IsNullOrEmpty(artifactType))
                {
                    ReportError(context, $"⚠️ {classSymbol.Name} 的 ArtifactType 未设置");
                    return;
                }

                // 生成代码
                GenerateCode(context, classMetadata, artifactType, templateName, overwrite);
            }
            catch (Exception ex)
            {
                LogDebug(context, $"⚠️ 处理 {classSymbol.Name} 时出错: {ex.Message}\n{ex.StackTrace}");
                ReportError(context, $"⚠️ 处理类 {classSymbol.Name} 失败: {ex.Message}");
            }
        }

        private ClassMetadata ExtractClassMetadata(INamedTypeSymbol classSymbol)
        {
            return new ClassMetadata
            {
                Namespace = classSymbol.ContainingNamespace.ToString(),
                Name = classSymbol.Name,
                FullName = $"{classSymbol.ContainingNamespace}.{classSymbol.Name}",
                Methods = classSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => !m.IsImplicitlyDeclared)
                    .Select(ExtractMethodMetadata)
                    .ToList()
            };
        }

        private MethodMetadata ExtractMethodMetadata(IMethodSymbol methodSymbol)
        {
            return new MethodMetadata
            {
                Name = methodSymbol.Name,
                ReturnType = methodSymbol.ReturnType.ToString(),
                IsAsync = methodSymbol.IsAsync,
                Parameters = methodSymbol.Parameters
                    .Select(ExtractParameterMetadata)
                    .ToList()
            };
        }

        private ParameterMetadata ExtractParameterMetadata(IParameterSymbol parameterSymbol)
        {
            return new ParameterMetadata
            {
                Name = parameterSymbol.Name,
                TypeName = parameterSymbol.Type.Name,
                TypeFullName = parameterSymbol.Type.ToString(),
                IsNullable = IsNullableType(parameterSymbol.Type),
                IsCollection = IsCollectionType(parameterSymbol.Type),
                CollectionItemType = GetCollectionItemType(parameterSymbol.Type)
            };
        }

        private void GenerateCode(SourceProductionContext context, ClassMetadata metadata,
            string artifactType, string templateName, bool overwrite)
        {
            var outputFileName = $"{metadata.Name}_{artifactType}.g.cs";
            LogDebug(context, $"🔅 准备生成文件: {outputFileName}");

            // 代码生成逻辑
            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("// <auto-generated>");
            codeBuilder.AppendLine($"// 自动生成于 {DateTime.UtcNow:o}");
            codeBuilder.AppendLine($"// 源类: {metadata.FullName}");
            codeBuilder.AppendLine($"// ArtifactType: {artifactType}");
            codeBuilder.AppendLine("// </auto-generated>");
            codeBuilder.AppendLine();
            codeBuilder.AppendLine($"namespace {metadata.Namespace}.Generated");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    public partial class {metadata.Name}Generated");
            codeBuilder.AppendLine("    {");
            codeBuilder.AppendLine($"        // 生成配置: Template={templateName}, Overwrite={overwrite}");
            codeBuilder.AppendLine($"        // 包含 {metadata.Methods.Count} 个方法");
            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("}");

            context.AddSource(outputFileName, SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
            LogDebug(context, $"💯 已生成文件: {outputFileName}");
        }

        #region 辅助方法
        private static bool IsCandidateClass(SyntaxNode node)
        {
            var isCandidate = node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Any();
            Debug.WriteLine($"IsCandidateClass: {node.GetType().Name} -> {isCandidate}");
            return isCandidate;
        }

        private INamedTypeSymbol GetClassSymbolWithDebug(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
            {
                LogDebug($"⚠️ 无法获取 {classDecl.Identifier.Text} 的符号信息");
                return null;
            }

            LogDebug($"🔅 发现类: {GetTypeFullName(classSymbol)}");
            return classSymbol;
        }

        // 获取类型的完全限定名
        private static string GetTypeFullName(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
                return typeSymbol.Name;
            return $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
        }

        private AttributeData GetGenerateAttribute(Compilation compilation, INamedTypeSymbol classSymbol)
        {
            var attributeType = compilation.GetTypeByMetadataName(GenerateArtifactAttributeFullName);
            if (attributeType == null)
            {
                LogDebug($"⚠️ 无法找到特性类型: {GenerateArtifactAttributeFullName}");
                return null;
            }

            return classSymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
        }

        private static string GetAttributeValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value?.ToString();
        }

        private static bool? GetAttributeBoolValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value as bool?;
        }

        private static bool IsNullableType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return true;

            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        private static bool IsCollectionType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol) return true;

            var namedType = type as INamedTypeSymbol;
            return namedType?.AllInterfaces.Any(i => i.ToString() == "System.Collections.Generic.IEnumerable`1") ?? false;
        }

        private static string GetCollectionItemType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arrayType)
                return arrayType.ElementType.Name;

            var namedType = type as INamedTypeSymbol;
            return namedType?.TypeArguments.Length > 0 ? namedType.TypeArguments[0].Name : null;
        }
        #endregion

        #region 调试日志（接口形式输出）
        private void LogDebug(string message)
        {
            var log = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            _debugLogs.Add(log);
            Debug.WriteLine($"[CodeGen] {log}");
        }

        private void LogDebug(SourceProductionContext context, string message)
        {
            LogDebug(message);
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "CG001", "代码生成调试", message, "CodeGen",
                    DiagnosticSeverity.Info, true),
                Location.None));
        }

        private void ReportError(SourceProductionContext context, string message)
        {
            LogDebug($"错误: {message}");
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "CG002", "代码生成错误", message, "CodeGen",
                    DiagnosticSeverity.Error, true),
                Location.None));
        }

        /// <summary>
        /// 生成结构化调试日志（接口形式）
        /// </summary>
        private void GenerateDebugLogFile(SourceProductionContext context)
        {
            var logContent = new StringBuilder();
            logContent.AppendLine("// <auto-generated> 代码生成调试日志 </auto-generated>");
            logContent.AppendLine("using System;");
            logContent.AppendLine("using System.Collections.Generic;");
            logContent.AppendLine();
            logContent.AppendLine("namespace CodeGen.DebugLogs {");
            logContent.AppendLine("    /// <summary>");
            logContent.AppendLine("    /// 代码生成过程调试日志接口");
            logContent.AppendLine("    /// 包含生成器执行的完整轨迹信息");
            logContent.AppendLine("    /// </summary>");
            logContent.AppendLine("    public interface ICodeGenerationLog {");
            logContent.AppendLine("        /// <summary>日志记录总数</summary>");
            logContent.AppendLine($"        int TotalLogs => {_debugLogs.Count};");
            logContent.AppendLine();
            logContent.AppendLine("        /// <summary>生成开始时间</summary>");
            logContent.AppendLine($"        DateTime StartTime => DateTime.Parse(\"{(_debugLogs.Count > 0 ? _debugLogs[0].Split(']')[0].TrimStart('[') : "00:00:00.000")}\");");
            logContent.AppendLine();
            logContent.AppendLine("        /// <summary>生成结束时间</summary>");
            logContent.AppendLine($"        DateTime EndTime => DateTime.Parse(\"{(_debugLogs.Count > 0 ? _debugLogs.Last().Split(']')[0].TrimStart('[') : "00:00:00.000")}\");");
            logContent.AppendLine();
            logContent.AppendLine("        /// <summary>执行时长（毫秒）</summary>");
            logContent.AppendLine($"        long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;");
            logContent.AppendLine();
            logContent.AppendLine("        /// <summary>是否包含错误</summary>");
            logContent.AppendLine($"        bool HasErrors => {_debugLogs.Any(l => l.Contains("错误:")).ToString().ToLower()};");
            logContent.AppendLine();
            logContent.AppendLine("        /// <summary>详细日志条目</summary>");
            logContent.AppendLine("        IEnumerable<string> LogEntries => new List<string> {");

            // 添加详细日志
            foreach (var log in _debugLogs)
            {
                logContent.AppendLine($"            \"{log.Replace("\"", "\\\"")}\",");
            }

            logContent.AppendLine("        };");
            logContent.AppendLine("    }");
            logContent.AppendLine();
            logContent.AppendLine("    /// <summary>");
            logContent.AppendLine("    /// 调试日志接口的默认实现");
            logContent.AppendLine("    /// </summary>");
            logContent.AppendLine("    public class CodeGenerationLog : ICodeGenerationLog { }");
            logContent.AppendLine("}");

            context.AddSource("CodeGen_DebugLog.g.cs",
                SourceText.From(logContent.ToString(), Encoding.UTF8));
        }
        #endregion
    }
}