using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Utilities;

namespace xCodeGen.SourceGenerator
{
    //[Generator]
    public class SourceMetaDataExtractor : IIncrementalGenerator
    {
        private static readonly string GenerateCodeAttributeFullName = GenerateCodeAttribute.TypeFullName;
        private readonly List<string> _debugLogs = new List<string>();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            LogDebug("⏱️ 开始初始化代码生成器");

            // 1. 筛选带有特性的类声明
            var candidateClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => CodeAnalysisDiagnostics.IsCandidateClass(node),
                    transform: (ctx, _) => ExtractGenerationInfo(ctx)
                )
                .Where(info => info != null);

            LogDebug("✅ 已创建类筛选数据流");

            // 2. 收集所有符合条件的类
            var allClasses = candidateClasses.Collect();

            // 3. 结合编译上下文
            context.CompilationProvider.Combine(allClasses);

            LogDebug("✅ 已组合编译上下文和类数据");

#if DEBUG
            //Debugger.Launch(); // 仅在调试时使用
            Debugger.Break();
#endif
            // 4. 注册代码生成输出处理每个类
            context.RegisterSourceOutput(candidateClasses.Collect(), (spc, classes) =>
            {
                LogDebug(spc, "⚛️ 进入代码生成阶段：");
                LogDebug(spc, $"⏱️ 开始生成 {classes.Length} 个类");
                foreach (var info in classes)
                {
                    LogDebug(spc, $"🔅 处理类: {info.Metadata.FullName}, Type: {info.Type}, Template: {info.TemplateName}, Overwrite: {info.Overwrite}");
                    try
                    {
                        GenerateCode(spc, info.Metadata, info.Type, info.TemplateName, info.Overwrite);
                    }
                    catch (Exception ex)
                    {
                        LogDebug(spc, $"\t⚠️ 生成过程异常: {ex.Message}\t{ex.StackTrace}");
                        ReportError(spc, $"\t⚠️ 生成器执行失败: {ex.Message}");
                    }
                }
                LogDebug(spc, "💯 代码生成过程完成。");
                GenerateDebugLogFile(spc);
            });


            LogDebug("✅ 初始化完成，等待生成触发");
        }
        private ClassGenerationInfo ExtractGenerationInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            // 只用字符串判断特性类型
            var generateAttribute = classSymbol?.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateCodeAttributeFullName);
            if (generateAttribute == null) return null;

            // 只用 AttributeData 提取参数，不依赖类型
            string type = null;
            var templateName = "Default";
            var overwrite = false;

            foreach (var arg in generateAttribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Type":
                        type = arg.Value.Value?.ToString();
                        break;
                    case "TemplateName":
                        templateName = arg.Value.Value?.ToString() ?? "Default";
                        break;
                    case "Overwrite":
                        overwrite = arg.Value.Value is bool b && b;
                        break;
                }
            }

            if (string.IsNullOrEmpty(type)) return null;

            var metadata = ExtractClassMetadata(classSymbol);

            return new ClassGenerationInfo
            {
                Metadata = metadata,
                Type = type,
                TemplateName = templateName,
                Overwrite = overwrite
            };
        }

        private ClassMetadata ExtractClassMetadata(INamedTypeSymbol classSymbol)
        {
            return new ClassMetadata
            {
                Namespace = classSymbol.ContainingNamespace.ToString(),
                ClassName = classSymbol.Name,
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
                IsNullable = CodeAnalysisDiagnostics.IsNullable(parameterSymbol.Type),
                IsCollection = CodeAnalysisHelper.IsCollectionType(parameterSymbol.Type),
                CollectionItemType = CodeAnalysisDiagnostics.GetCollectionItemType(parameterSymbol.Type)
            };
        }

        private void GenerateCode(SourceProductionContext context, ClassMetadata metadata,
            string type, string templateName, bool overwrite)
        {
            var outputFileName = $"Meta/{metadata.ClassName}_{type}.g.cs";
            LogDebug(context, $"🔅 准备生成文件: {outputFileName}");

            // 代码生成逻辑
            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("// <auto-generated>");
            codeBuilder.AppendLine($"// 自动生成于 {DateTime.UtcNow:o}");
            codeBuilder.AppendLine($"// 源类: {metadata.FullName}");
            codeBuilder.AppendLine($"// Type: {type}");
            codeBuilder.AppendLine("// </auto-generated>");
            codeBuilder.AppendLine();
            codeBuilder.AppendLine($"namespace {metadata.Namespace}.Generated");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    public partial class {metadata.ClassName}Generated");
            codeBuilder.AppendLine("    {");
            codeBuilder.AppendLine($"        // 生成配置: Template={templateName}, Overwrite={overwrite}");
            codeBuilder.AppendLine($"        // 包含 {metadata.Methods.Count} 个方法");
            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("}");

            context.AddSource(outputFileName, SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
            LogDebug(context, $"💯 已生成文件: {outputFileName}");
        }

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
            logContent.AppendLine("    public partial interface ICodeGenerationLog {");
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
            logContent.AppendLine("        long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;");
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
            // logContent.AppendLine("    /// <summary>");
            // logContent.AppendLine("    /// 调试日志接口的默认实现");
            // logContent.AppendLine("    /// </summary>");
            // logContent.AppendLine("    public class CodeGenerationLog : ICodeGenerationLog { }");
            logContent.AppendLine("}");

            context.AddSource("Logs/xCodeGen_DebugLog.g.cs",
                SourceText.From(logContent.ToString(), Encoding.UTF8));
        }
        #endregion
    }
}