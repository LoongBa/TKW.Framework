using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace xCodeGen.Core.Utilities
{
    /// <summary>
    /// 提供代码生成相关的工具方法
    /// </summary>
    public static class CodeAnalysisHelper
    {
        private static readonly object _propertyDictionaryLock = new object();
        private static Dictionary<string, string> _propertyDictionary;
        private static readonly string[] CollectionTypeNames = { "IEnumerable", "ICollection", "IList", "List", "Array" };

        /// <summary>
        /// 读取 MSBuild 属性
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值</returns>
        public static string ReadMsBuildProperty(in AnalyzerConfigOptionsProvider options, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("属性名称不能为空", nameof(propertyName));

            var key = $"build_property.{propertyName}";

            // 双重检查锁定模式实现线程安全的懒加载
            if (_propertyDictionary == null)
            {
                lock (_propertyDictionaryLock)
                {
                    if (_propertyDictionary == null)
                    {
                        _propertyDictionary = GetAllProperties(options);
                    }
                }
            }

            return _propertyDictionary.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// 获取所有属性
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <returns>属性字典</returns>
        private static Dictionary<string, string> GetAllProperties(in AnalyzerConfigOptionsProvider options)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (options == null)
                return result;

            foreach (var key in options.GlobalOptions.Keys)
                if (options.GlobalOptions.TryGetValue(key, out var value))
                    result[key] = value;

            return result;
        }
        
        /// <summary>
        /// 检查类型是否为可空类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>如果类型是可空的返回 true，否则返回 false</returns>
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
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        /// <summary>
        /// 检查类型是否为集合类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>如果类型是集合类型返回 true，否则返回 false</returns>
        public static bool IsCollectionType(ITypeSymbol type)
        {
            if (type == null)
                return false;

            if (type.TypeKind == TypeKind.Array)
                return true;

            if (type is INamedTypeSymbol namedType)
            {
                return namedType.AllInterfaces.Any(i => i.ToDisplayString().Contains("IEnumerable")) ||
                       CollectionTypeNames.Any(name => namedType.ToDisplayString().Contains(name));
            }

            return false;
        }

        /// <summary>
        /// 获取项目目录
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>项目目录路径</returns>
        /// <summary>
        /// 获取项目根目录（.csproj 所在目录）
        /// </summary>
        public static string GetProjectDirectory(
            in AnalyzerConfigOptionsProvider options,
            Compilation compilation)
        {
            // 1. 优先使用 MSBuild 内置属性（最可靠，直接返回项目根目录）
            var msbuildProjectDir = ReadMsBuildProperty(options, "MSBuildProjectDirectory");
            if (!string.IsNullOrWhiteSpace(msbuildProjectDir))
            {
                // 验证路径是否包含典型编译输出目录，增强容错性
                // 修正：使用 IndexOf 替代 Contains，兼容不支持 StringComparison 参数的框架版本
                if (msbuildProjectDir.IndexOf("obj", StringComparison.OrdinalIgnoreCase) == -1 &&
                    msbuildProjectDir.IndexOf("bin", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return msbuildProjectDir;
                }
            }

            // 2. 从项目文件路径推导（.csproj 所在目录即为根目录）
            var msbuildProjectFile = ReadMsBuildProperty(options, "MSBuildProjectFile");
            if (!string.IsNullOrWhiteSpace(msbuildProjectFile) &&
                string.Equals(Path.GetExtension(msbuildProjectFile), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var projectDir = Path.GetDirectoryName(msbuildProjectFile);
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
        /// 从特性数据中提取生成相关参数
        /// </summary>
        /// <param name="attribute">要分析的特性数据</param>
        /// <returns>
        /// 返回一个元组，包含三个元素：
        /// - Type: 目标类型名称
        /// - TemplateName: 模板名称，默认为 "Default"
        /// - Overwrite: 是否覆盖现有文件，默认为 false
        /// </returns>
        public static (string Type, string TemplateName, bool Overwrite) ExtractGenerateAttributeParams(AttributeData attribute)
        {
            if (attribute == null)
                throw new ArgumentNullException(nameof(attribute));

            // 使用命名参数访问
            var type = GetNamedArgumentValue<string>(attribute, "Type") ??
                       GetConstructorArgumentValue<string>(attribute, 0);

            var templateName = GetNamedArgumentValue<string>(attribute, "TemplateName") ??
                               GetConstructorArgumentValue<string>(attribute, 1) ??
                               "Default";

            // 对于布尔值，使用可空类型
            bool? overwrite = GetNamedArgumentValue<bool>(attribute, "Overwrite");

            return (type, templateName, (bool)overwrite);
        }
        
        private static T GetNamedArgumentValue<T>(AttributeData attribute, string argumentName)
        {
            if (attribute == null)
                return default(T);

            var namedArgument = attribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == argumentName);

            // 检查 TypedConstant 是否有值
            if (namedArgument.Value.Kind != TypedConstantKind.Error &&
                namedArgument.Value.Value is T value)
                return value;

            return default(T);
        }
        
        private static T GetConstructorArgumentValue<T>(AttributeData attribute, int index)
        {
            if (attribute == null)
                return default(T);

            if (index < 0 || index >= attribute.ConstructorArguments.Length)
                return default(T);

            if (attribute.ConstructorArguments[index].Value is T value)
                return value;

            return default(T);
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
            var outputPath = ReadMsBuildProperty(options, "OutputPath");
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return string.IsNullOrWhiteSpace(projectDir)
                    ? outputPath
                    : Path.Combine(projectDir, outputPath);
            }

            // 2. 兜底：项目目录 + "bin/Debug"
            var defaultRelativePath = $"bin{Path.DirectorySeparatorChar}{GetBuildConfiguration(options)}";
            return string.IsNullOrWhiteSpace(projectDir)
                ? defaultRelativePath
                : Path.Combine(projectDir, defaultRelativePath);
        }

        private static string ComputeOutputPath(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var outputPath = ReadMsBuildProperty(options, "OutputPath");
            if (string.IsNullOrEmpty(outputPath))
            {
                var projectDirectory = GetProjectDirectory(options, compilation);
                outputPath = System.IO.Path.Combine(projectDirectory, "Generated");
            }

            return outputPath;
        }

        /// <summary>
        /// 获取构建配置
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <returns>构建配置名称</returns>
        public static string GetBuildConfiguration(in AnalyzerConfigOptionsProvider options)
        {
            return ReadMsBuildProperty(options, "Configuration") ?? "Debug";
        }

        /// <summary>
        /// 获取目标框架
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <returns>目标框架名称</returns>
        public static string GetTargetFramework(in AnalyzerConfigOptionsProvider options)
        {
            return ReadMsBuildProperty(options, "TargetFramework") ?? "net10.0";
        }

        /// <summary>
        /// 获取根命名空间
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>根命名空间</returns>
        public static string GetRootNamespace(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var rootNamespace = ReadMsBuildProperty(options, "RootNamespace");
            if (string.IsNullOrEmpty(rootNamespace))
            {
                rootNamespace = compilation.AssemblyName;
            }

            return rootNamespace;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        public static void ValidateConfiguration(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var targetFramework = GetTargetFramework(options);
            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                throw new InvalidOperationException("未配置目标框架(TargetFramework)");
            }

            var outputPath = GetOutputPath(options, compilation);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException("输出路径(OutputPath)无效");
            }
        }

        /// <summary>
        /// 记录配置信息
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        public static void LogConfiguration(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            Debug.WriteLine("=== 当前配置 ===");
            Debug.WriteLine($"目标框架: {GetTargetFramework(options)}");
            Debug.WriteLine($"输出路径: {GetOutputPath(options, compilation)}");
            Debug.WriteLine($"根命名空间: {GetRootNamespace(options, compilation)}");
            Debug.WriteLine($"构建配置: {GetBuildConfiguration(options)}");
            Debug.WriteLine("================");
        }

        /// <summary>
        /// 获取带有调试信息的类符号
        /// </summary>
        /// <param name="compilation">编译信息</param>
        /// <param name="type">类型名称</param>
        /// <returns>类符号</returns>
        public static INamedTypeSymbol GetClassSymbolWithDebug(Compilation compilation, string type)
        {
            var typeSymbol = compilation.GetTypeByMetadataName(type);
            if (typeSymbol == null)
            {
                Debug.WriteLine($"无法找到类型: {type}");
            }
            return typeSymbol;
        }

        /// <summary>
        /// 获取程序集名称
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>程序集名称</returns>
        public static string GetAssemblyName(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            // 优先从 MSBuild 属性获取
            var assemblyName = ReadMsBuildProperty(options, "AssemblyName");
            if (!string.IsNullOrEmpty(assemblyName))
                return assemblyName;

            // 如果没有配置，使用编译对象的程序集名称
            return compilation.AssemblyName;
        }

        /// <summary>
        /// 获取 Nullable 引用类型配置
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>Nullable 配置值（disable/enable/warnings/annotations）</returns>
        public static string GetNullableConfig(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            // 优先从 MSBuild 属性获取
            var nullableConfig = ReadMsBuildProperty(options, "Nullable");
            if (!string.IsNullOrEmpty(nullableConfig))
                return nullableConfig;

            // 如果没有配置，检查编译选项
            if (compilation.Options is CSharpCompilationOptions csharpOptions)
            {
                switch (csharpOptions.NullableContextOptions)
                {
                    case NullableContextOptions.Enable:
                        return "enable";
                    case NullableContextOptions.Warnings:
                        return "warnings";
                    case NullableContextOptions.Annotations:
                        return "annotations";
                    default:
                        return "disable";
                }
            }

            // 默认值
            return "disable";
        }

        /// <summary>
        /// 获取 C# 语言版本
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>C# 语言版本（如 7.3、10.0）</returns>
        public static string GetLangVersion(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            // 优先从 MSBuild 属性获取
            var langVersion = ReadMsBuildProperty(options, "LangVersion");
            if (!string.IsNullOrEmpty(langVersion))
                return langVersion;

            if (compilation is CSharpCompilation csharpCompilation)
            {
                switch (csharpCompilation.LanguageVersion)
                {
                    case LanguageVersion.CSharp1:
                        return "1.0";
                    case LanguageVersion.CSharp2:
                        return "2.0";
                    case LanguageVersion.CSharp3:
                        return "3.0";
                    case LanguageVersion.CSharp4:
                        return "4.0";
                    case LanguageVersion.CSharp5:
                        return "5.0";
                    case LanguageVersion.CSharp6:
                        return "6.0";
                    case LanguageVersion.CSharp7:
                        return "7.0";
                    case LanguageVersion.CSharp7_1:
                        return "7.1";
                    case LanguageVersion.CSharp7_2:
                        return "7.2";
                    case LanguageVersion.CSharp7_3:
                        return "7.3";
                    case LanguageVersion.CSharp8:
                        return "8.0";
                    case LanguageVersion.CSharp9:
                        return "9.0";
                    case LanguageVersion.CSharp10:
                        return "10.0";
                    case LanguageVersion.CSharp11:
                        return "11.0";
                    case LanguageVersion.CSharp12:
                        return "12.0";
                    case LanguageVersion.CSharp13:
                        return "13.0";
                    default:
                        return "latest";
                }
            }

            // 默认值
            return "latest";
        }

        /// <summary>
        /// 获取生成文件的存放目录
        /// </summary>
        /// <param name="options">配置选项提供者</param>
        /// <param name="compilation">编译信息</param>
        /// <returns>生成文件的存放目录（绝对路径）</returns>
        public static string GetGeneratedFilesDirectory(in AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (compilation == null)
                throw new ArgumentNullException(nameof(compilation));

            // 优先从 MSBuild 属性获取
            var generatedFilesPath = ReadMsBuildProperty(options, "CompilerGeneratedFilesOutputPath");
            if (!string.IsNullOrEmpty(generatedFilesPath))
                return generatedFilesPath;

            // 如果没有配置，使用默认路径
            var outputPath = GetOutputPath(options, compilation);
            return System.IO.Path.Combine(outputPath, "Generated");
        }
        /// <summary>
        /// 获取生成代码的基础命名空间
        /// </summary>
        /// <param name="rootNamespace">项目根命名空间</param>
        /// <returns>生成代码的基础命名空间</returns>
        /// <remarks>如果根命名空间为空则使用默认值
        /// 默认在根命名空间后添加 .Generated 后缀</remarks>
        public static string GetGeneratedRootNamespace(string rootNamespace)
        {
            if (string.IsNullOrEmpty(rootNamespace))
                return "Generated";

            // 默认在根命名空间后添加 .Generated 后缀
            return $"{rootNamespace}.Generated";
        }

    }
}
