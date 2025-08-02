// 注意：本文件需遵循 C# 7.3 语法标准，请勿使用更高版本特性（如模式匹配、空值判断运算符等）
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Core
{
    /// <summary>
    /// 代码分析辅助工具类，封装公共的语法分析逻辑
    /// </summary>
    public static class CodeAnalysisHelper
    {
        #region 项目配置相关方法

        /// <summary>
        /// 获取项目根目录（.csproj 所在目录）
        /// </summary>
        public static string GetProjectDirectory(
            in AnalyzerConfigOptionsProvider options,
            Compilation compilation)
        {
            // 1. 优先使用 MSBuild 内置属性（最可靠，直接返回项目根目录）
            if (options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var dir)
                && !string.IsNullOrWhiteSpace(dir))
            {
                // 验证路径是否包含典型编译输出目录，增强容错性
                // 修正：使用 IndexOf 替代 Contains，兼容不支持 StringComparison 参数的框架版本
                if (dir.IndexOf("obj", StringComparison.OrdinalIgnoreCase) == -1 &&
                    dir.IndexOf("bin", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return dir;
                }
            }

            // 2. 从项目文件路径推导（.csproj 所在目录即为根目录）
            if (options.GlobalOptions.TryGetValue("build_property.MSBuildProjectFile", out var projectFile)
                && !string.IsNullOrWhiteSpace(projectFile) &&
                string.Equals(Path.GetExtension(projectFile), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var projectDir = Path.GetDirectoryName(projectFile);
                if (!string.IsNullOrWhiteSpace(projectDir) &&
                    projectDir.IndexOf("obj", StringComparison.OrdinalIgnoreCase) == -1 &&
                    projectDir.IndexOf("bin", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return projectDir;
                }
            }

            // 3. 仅通过源文件路径的字符串特征推导（无文件系统操作）
            if (compilation.SyntaxTrees.Any())
            {
                var firstSourcePath = compilation.SyntaxTrees.First().FilePath;
                if (!string.IsNullOrWhiteSpace(firstSourcePath))
                {
                    var pathSeparator = Path.DirectorySeparatorChar;
                    var dirSegments = firstSourcePath.Split(new[] { pathSeparator }, StringSplitOptions.RemoveEmptyEntries);

                    // 过滤关键优化：识别并排除编译输出目录（obj/bin）
                    var outputDirIndices = new List<int>();
                    for (int i = 0; i < dirSegments.Length; i++)
                    {
                        if (string.Equals(dirSegments[i], "obj", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(dirSegments[i], "bin", StringComparison.OrdinalIgnoreCase))
                        {
                            outputDirIndices.Add(i);
                        }
                    }

                    // 如果存在编译输出目录，取其之前的路径作为根目录
                    if (outputDirIndices.Any())
                    {
                        var firstOutputDirIndex = outputDirIndices.First();
                        if (firstOutputDirIndex > 0)
                        {
                            return string.Join(pathSeparator.ToString(),
                                dirSegments.Take(firstOutputDirIndex));
                        }
                    }

                    // 原有策略：寻找 src 目录
                    for (int i = 0; i < dirSegments.Length; i++)
                    {
                        if (string.Equals(dirSegments[i], "src", StringComparison.OrdinalIgnoreCase))
                        {
                            if (i + 1 < dirSegments.Length)
                            {
                                return string.Join(pathSeparator.ToString(),
                                    dirSegments.Take(i + 2));
                            }
                            return string.Join(pathSeparator.ToString(),
                                dirSegments.Take(i + 1));
                        }
                    }

                    // 备选策略：取源文件路径的上两级目录
                    if (dirSegments.Length >= 2)
                    {
                        return string.Join(pathSeparator.ToString(),
                            dirSegments.Take(dirSegments.Length - 2));
                    }

                    return Path.GetDirectoryName(firstSourcePath) ?? string.Empty;
                }
            }

            // 4. 最终兜底
            return string.Empty;
        }


        /// <summary>
        /// 获取编译输出目录（绝对路径）
        /// </summary>
        public static string GetOutputPath(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // 1. 优先使用项目配置的 OutputPath
            var projectDir = GetProjectDirectory(options, compilation);
            if (options.GlobalOptions.TryGetValue("build_property.OutputPath", out var outputPath)
                && !string.IsNullOrWhiteSpace(outputPath))
            {
                return string.IsNullOrWhiteSpace(projectDir)
                    ? outputPath
                    : Path.Combine(projectDir, outputPath);
            }

            // 3. 兜底：项目目录 + "bin/Debug"
            var defaultRelativePath = $"bin{Path.DirectorySeparatorChar}{GetBuildConfiguration(options)}";
            var defaultProjectDir = projectDir;
            return string.IsNullOrWhiteSpace(defaultProjectDir)
                ? defaultRelativePath
                : Path.Combine(defaultProjectDir, defaultRelativePath);
        }

        /// <summary>
        /// 获取生成文件的最终存放路径（绝对路径）
        /// <remarks>优先取项目配置 "CompilerGeneratedFilesOutputPath" 的值</remarks>
        /// <remarks>其次，默认为项目输出目录下的 Generated 文件夹</remarks>
        /// </summary>
        public static string GetGeneratedFilesDirectory(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // 优先使用用户配置的生成路径
            if (options.GlobalOptions.TryGetValue("build_property.CompilerGeneratedFilesOutputPath", out var customPath)
                && !string.IsNullOrWhiteSpace(customPath))
            {
                var projectDir = GetProjectDirectory(options, compilation);
                return string.IsNullOrWhiteSpace(projectDir)
                    ? customPath
                    : Path.Combine(projectDir, customPath);
            }

            // 否则使用 OutputPath/generated
            return Path.Combine(GetOutputPath(options, compilation), "generated");
        }

        /// <summary>
        /// 获取根命名空间
        /// </summary>
        public static string GetRootNamespace(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            if (options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNs) && !string.IsNullOrWhiteSpace(rootNs))
                return rootNs;

            return GetAssemblyName(options, compilation) ?? "xCodeGen.Generated";
        }

        /// <summary>
        /// 获取程序集名称
        /// </summary>
        public static string GetAssemblyName(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            if (options.GlobalOptions.TryGetValue("build_property.AssemblyName", out var assemblyName))
                return assemblyName;

            return compilation.AssemblyName ?? "xCodeGen.Generated";
        }

        /// <summary>
        /// 获取目标框架
        /// </summary>
        public static string GetTargetFramework(in AnalyzerConfigOptionsProvider options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            options.GlobalOptions.TryGetValue("build_property.TargetFramework", out var tfm);
            return tfm ?? string.Empty;
        }

        /// <summary>
        /// 获取编译配置（Debug/Release）
        /// </summary>
        public static string GetBuildConfiguration(AnalyzerConfigOptionsProvider options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.GlobalOptions.TryGetValue("build_property.Configuration", out var config) && !string.IsNullOrWhiteSpace(config))
                return config;
            // 默认返回 Debug
            return "Debug";
        }

        /// <summary>
        /// 获取语言版本
        /// </summary>
        public static string GetLangVersion(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));
            // 优先使用 MSBuild 属性
            if (options.GlobalOptions.TryGetValue("build_property.LanguageVersion", out var langVersion) && !string.IsNullOrWhiteSpace(langVersion))
                return langVersion;
            // 其次使用编译选项
            if (compilation is CSharpCompilation csharpCompilation)
            {
                return csharpCompilation.LanguageVersion.ToString();
            }
            // 默认返回 C# 7.3
            return "7.3";
        }

        /// <summary>
        /// 获取可空类型配置
        /// </summary>
        public static string GetNullableConfig(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            if (options.GlobalOptions.TryGetValue("build_property.Nullable", out var nullable) && !string.IsNullOrWhiteSpace(nullable))
                return nullable.ToLower();

            if (compilation is CSharpCompilation csharpCompilation)
            {
                return csharpCompilation.Options.NullableContextOptions.ToString().ToLower();
            }

            return "disable";
        }

        /// <summary>
        /// 计算业务根命名空间（从元数据推断）
        /// </summary>
        public static string GetGeneratedRootNamespace(string rootNamespace)
        {
            if (string.IsNullOrEmpty(rootNamespace))
                return "xCodeGen.Generated";
            return $"{rootNamespace}.Generated";
        }

        /// <summary>
        /// 读取MSBuild属性值
        /// </summary>
        public static string ReadMsBuildProperty(in AnalyzerConfigOptionsProvider options, string propertyName)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("属性名称不能为空", nameof(propertyName));

            options.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value);
            return value ?? string.Empty;
        }

        #endregion

        #region 类型与特性分析相关方法

        /// <summary>
        /// 从特性数据中提取生成相关参数
        /// </summary>
        public static (string Type, string TemplateName, bool Overwrite) ExtractGenerateAttributeParams(AttributeData attribute)
        {
            string type = null;
            var templateName = "Default";
            var overwrite = false;

            foreach (var arg in attribute.NamedArguments)
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

            return (type, templateName, overwrite);
        }

        /// <summary>
        /// 将访问修饰符转换为字符串表示
        /// </summary>
        public static string GetAccessModifier(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public:
                    return "public";
                case Accessibility.Private:
                    return "private";
                case Accessibility.Protected:
                    return "protected";
                case Accessibility.Internal:
                    return "internal";
                case Accessibility.ProtectedOrInternal:
                    return "protected internal";
                case Accessibility.ProtectedAndInternal:
                    return "private protected";
                default:
                    return "private";
            }
        }

        /// <summary>
        /// 获取集合元素类型（数组元素类型或泛型集合的元素类型）
        /// </summary>
        public static string GetCollectionItemType(ITypeSymbol type)
        {
            // 数组类型
            if (type is IArrayTypeSymbol arrayType)
                return arrayType.ElementType.ToDisplayString();

            // 泛型集合类型
            var namedType = type as INamedTypeSymbol;
            return namedType?.TypeArguments.Length > 0
                ? namedType.TypeArguments[0].ToDisplayString()
                : null;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public static string GetAttributeValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value?.ToString();
        }

        /// <summary>
        /// 获取属性的 bool 值
        /// </summary>
        public static bool GetAttributeBoolValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value is bool value && value;
        }

        /// <summary>
        /// 从语法上下文获取类符号并记录调试信息
        /// </summary>
        public static INamedTypeSymbol GetClassSymbolWithDebug(GeneratorSyntaxContext context, Action<string> logDebug)
        {
            var classDecl = context.Node as ClassDeclarationSyntax;
            if (classDecl == null)
                return null;

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if (classSymbol == null)
            {
                if (logDebug != null)
                    logDebug($"⚠️ 无法获取 {classDecl.Identifier.Text} 的符号信息");
                return null;
            }

            if (logDebug != null)
                logDebug($"🔅 发现类: {GetTypeFullName(classSymbol)}");
            return classSymbol;
        }

        /// <summary>
        /// 获取生成特性的符号信息
        /// </summary>
        public static AttributeData GetGenerateAttribute(Compilation compilation, INamedTypeSymbol classSymbol, string attributeFullName)
        {
            var attributeType = compilation.GetTypeByMetadataName(attributeFullName);
            if (attributeType == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 无法找到特性类型: {attributeFullName}");
                return null;
            }

            return classSymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
        }

        /// <summary>
        /// 获取类型的完全限定名（包含命名空间）
        /// </summary>
        public static string GetTypeFullName(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
                return typeSymbol.Name;
            return $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
        }

        /// <summary>
        /// 检查类是否标记了[GenerateCode]特性
        /// </summary>
        public static bool HasGenerateCodeAttribute(Compilation compilation, ClassDeclarationSyntax classDecl)
        {
            var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            return classSymbol?.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName) ?? false;
        }

        /// <summary>
        /// 判断类型是否为可空类型（可空值类型或可空引用类型）
        /// </summary>
        /// <param name="type">类型符号</param>
        /// <returns>如果是可空类型则返回 true，否则返回 false</returns>
        public static bool IsNullable(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // 处理可空值类型（如 int?、DateTime?）
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var originalDefinition = namedType.OriginalDefinition;
                if (originalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    return true;
                }
            }

            // 处理可空引用类型（C# 8.0+）
            if (type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断语法节点是否为候选类（类声明且包含特性）
        /// </summary>
        public static bool IsCandidateClass(SyntaxNode node)
        {
            var isCandidate = node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Any();
            System.Diagnostics.Debug.WriteLine($"IsCandidateClass: {node.GetType().Name} -> {isCandidate}");
            return isCandidate;
        }

        /// <summary>
        /// 判断类型是否为集合类型（数组或实现了IEnumerable）
        /// </summary>
        public static bool IsCollectionType(ITypeSymbol type)
        {
            // 数组类型
            if (type is IArrayTypeSymbol) return true;

            // 实现了 IEnumerable<T> 的类型
            var namedType = type as INamedTypeSymbol;
            return namedType?.AllInterfaces.Any(i =>
                i.ToDisplayString() == "System.Collections.Generic.IEnumerable`1") ?? false;
        }

        /// <summary>
        /// 判断类型是否为可空类型
        /// </summary>
        public static bool IsNullableType(ITypeSymbol type)
        {
            // 处理 Nullable<T> 类型
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return true;

            // 处理可为空引用类型（C# 8.0+）
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        /// <summary>
        /// 判断方法是否为特殊方法（属性访问器、事件访问器、运算符、构造函数等）
        /// </summary>
        public static bool IsSpecialMethod(IMethodSymbol method)
        {
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) return true; // 属性访问器
            if (method.Name.StartsWith("add_") || method.Name.StartsWith("remove_")) return true; // 事件访问器
            if (method.Name.StartsWith("op_")) return true; // 运算符重载
            if (method.MethodKind == MethodKind.Constructor || method.MethodKind == MethodKind.Destructor) return true; // 构造/析构函数
            return false;
        }

        #endregion
    }
}