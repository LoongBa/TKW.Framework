using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using xCodeGen.Abstractions.Attributes;

// 保留 Roslyn 4.0+ 的 [Generator] 特性
namespace xCodeGen.SourceGenerator
{
    //[Generator]
    // 保留 IIncrementalGenerator 接口（Roslyn 4.0+ 增量生成器特性）
    public sealed class DtoGenerator : IIncrementalGenerator
    {
        static DtoGenerator()
        {
            GenerateDtoAttributeFullName = GenerateArtifactAttribute.TypeFullName;
        }

        private static readonly string GenerateDtoAttributeFullName;

        // 用于累积多条日志的列表
        private readonly List<string> _debugMessages = new List<string>();

        // 统一的调试日志方法
        private void LogDebug(SourceProductionContext context, string message)
        {
            // 1. 输出到调试窗口
            Debug.WriteLine($"[DtoGenerator] {message}");

            // 2. 收集日志用于后续输出
            _debugMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

            // 3. 报告为诊断信息（在错误列表中可见）
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DTO001",
                    "DTO Generator Debug",
                    message,
                    "DtoGenerator",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None));
        }

        // 重载 LogDebug 方法以支持 GeneratorSyntaxContext
        private void LogDebug(GeneratorSyntaxContext context, string message)
        {
            // 对于 GeneratorSyntaxContext，我们没有直接的 ReportDiagnostic
            // 所以只记录到调试窗口和内存列表
            Debug.WriteLine($"[DtoGenerator] {message}");
            _debugMessages.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
        }

        // 报告错误
        private void ReportError(SourceProductionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DTO002",
                    "DTO Generator Error",
                    message,
                    "DtoGenerator",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None));
        }

        // 生成调试日志文件（作为有效的C#文件）
        private void GenerateDebugLogFile(SourceProductionContext context)
        {
            if (_debugMessages.Count == 0)
                return;

            // 构建日志内容（格式化为C#注释）
            var logContent = new StringBuilder();
            logContent.AppendLine("// This is a generated debug log file.");
            logContent.AppendLine("// It contains diagnostic information from the DTO generator.");
            logContent.AppendLine("// This file is for debugging purposes only and does not affect compilation.");
            logContent.AppendLine();

            foreach (var message in _debugMessages)
            {
                logContent.AppendLine($"// {message}");
            }

            // 添加一个空的命名空间，使其成为有效的C#文件
            logContent.AppendLine("namespace DtoGeneratorDebug { }");

            // 生成文件（.g.cs扩展名会被编译器处理）
            context.AddSource("DtoGeneratorDebug.g.cs", SourceText.From(
                logContent.ToString(),
                Encoding.UTF8));
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. 扫描所有被 [GenerateDto] 标记的类型
            var attributedTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    (node, _) => IsCandidateClass(node),
                    GetAttributedTypeSymbol)
                .Where(type => type != null);

            // 2. 结合编译上下文
            var compilationAndTypes = context.CompilationProvider
                .Combine(attributedTypes.Collect());

            // 3. 生成DTO代码
            context.RegisterSourceOutput(compilationAndTypes, (spc, source) =>
            {
                GenerateDtoClasses(source.Left, source.Right, spc);
            });
        }

        private void GenerateDtoClasses(
            Compilation compilation,
            ImmutableArray<INamedTypeSymbol> types,
            SourceProductionContext context)
        {
            LogDebug(context, $"Processing {types.Length} types");

            foreach (var type in types)
            {
                LogDebug(context, $"\t***Processing {type.Name} DtoClasses.");
                try
                {
                    // 确保特性存在
                    var attribute = type.GetAttributes()
                        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(
                            a.AttributeClass,
                            compilation.GetTypeByMetadataName(GenerateDtoAttributeFullName)));

                    if (attribute == null)
                    {
                        LogDebug(context, $"Warning: {type.Name} does not have [GenerateDto] attribute");
                        continue;
                    }

                    // 安全获取特性参数
                    /*
                    var dtoNamespace = attribute.GetNamedArgument<string>("Namespace")
                        ?? $"{type.ContainingNamespace}.Dtos";

                    var dtoSuffix = attribute.GetNamedArgument<string>("Suffix")
                        ?? "Dto";
                        */
                    var dtoNamespace = $"{type.ContainingNamespace}.Dtos";
                    var dtoSuffix = "Dto";
                    var dtoName = $"{type.Name}{dtoSuffix}";
                    var sourceCode = GenerateDtoSource(type, dtoNamespace, dtoName);
                    context.AddSource($"{dtoName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));

                    LogDebug(context, $"✅ Generated {dtoName}.g.cs");
                }
                catch (Exception ex)
                {
                    LogDebug(context, $"***DTO Generation Error:{ex.Message}\r\n\t{ex}");

                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DTO001",
                            "DTO Generation Error",
                            $"Failed to generate DTO for {type.Name}: {ex.Message}",
                            "DtoGenerator",
                            DiagnosticSeverity.Error,
                            true),
                        type.Locations.FirstOrDefault() ?? Location.None));
                }
            }

            GenerateDebugLogFile(context);
        }

        // 辅助方法：判断节点是否为候选类（替代递归模式匹配）
        private static bool IsCandidateClass(SyntaxNode node)
        {
            if (node is ClassDeclarationSyntax classDecl)
            {
                // 检查类是否有属性列表
                return classDecl.AttributeLists.Count > 0;
            }
            return false;
        }

        private INamedTypeSymbol GetAttributedTypeSymbol(GeneratorSyntaxContext context, CancellationToken ct)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                return null;

            LogDebug(context, $"Checking class: {classSymbol.ToDisplayString()}");

            // 获取特性类型
            var attributeType = context.SemanticModel.Compilation.GetTypeByMetadataName(
                "TKW.Framework.Domain.SourceGenerator.Attributes.GenerateDtoAttribute");

            if (attributeType == null)
            {
                LogDebug(context, "⚠️ Could not find GenerateDtoAttribute type");
                return null;
            }

            LogDebug(context, $"GenerateDtoAttribute full name: {attributeType.ToDisplayString()}");

            // 输出类上的所有特性
            foreach (var attr in classSymbol.GetAttributes())
            {
                LogDebug(context, $"  Found attribute: {attr.AttributeClass?.ToDisplayString()}");
            }

            // 检查特性
            var attribute = classSymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));

            if (attribute != null)
            {
                LogDebug(context, $"✅ Found [GenerateDto] on {classSymbol.Name}");
                return classSymbol;
            }

            LogDebug(context, $"⚠️ {classSymbol.Name} does not have [GenerateDto] attribute");
            return null;
        }

        private static string GenerateDtoSource(INamedTypeSymbol type, string dtoNamespace, string dtoName)
        {
            // 移除非确定性代码（DateTime.Now 会导致 Roslyn 警告）
            var properties = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public)
                .ToList();

            var source = new StringBuilder();
            source.AppendLine($"// <auto-generated>");
            source.AppendLine($"// 此文件由 DtoGenerator 自动生成，请勿手动修改");
            source.AppendLine($"// 源类型: {type.ToDisplayString()}");
            source.AppendLine($"// </auto-generated>"); // 移除 DateTime.Now
            source.AppendLine();
            source.AppendLine($"using System;");
            source.AppendLine();
            source.AppendLine($"namespace {dtoNamespace}");
            source.AppendLine($"{{");
            source.AppendLine($"    /// <summary>");
            source.AppendLine($"    /// 对应 {type.Name} 的数据传输对象");
            source.AppendLine($"    /// </summary>");
            source.AppendLine($"    public partial class {dtoName}");
            source.AppendLine($"    {{");

            foreach (var property in properties)
            {
                source.AppendLine($"        public {property.Type.ToDisplayString()} {property.Name} {{ get; set; }}");
            }

            source.AppendLine($"    }}");
            source.AppendLine($"}}");

            return source.ToString();
        }
    }
}

