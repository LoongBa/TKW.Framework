#pragma warning disable IDE0240
#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
// ReSharper disable RedundantExplicitArrayCreation
#pragma warning disable IDE0300

namespace xCodeGen.SourceGenerator.Utilities
{
    /// <summary>
    /// 提供代码生成相关的工具方法 (C# 7.3 兼容版)
    /// </summary>
    public static class CodeAnalysisHelper
    {
        private static readonly object _propertyDictionaryLock = new object();
        private static Dictionary<string, string> _propertyDictionary;
        private static readonly string[] _collectionTypeNames = new string[] { "IEnumerable", "ICollection", "IList", "List", "Array" };

        /// <summary>
        /// 读取 MSBuild 属性
        /// </summary>
        public static string ReadMsBuildProperty(AnalyzerConfigOptionsProvider options, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("属性名称不能为空", nameof(propertyName));

            var key = "build_property." + propertyName;

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

            string value;
            return _propertyDictionary.TryGetValue(key, out value) ? value : null;
        }

        private static Dictionary<string, string> GetAllProperties(AnalyzerConfigOptionsProvider options)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (options == null) return result;

            foreach (var key in options.GlobalOptions.Keys)
                if (options.GlobalOptions.TryGetValue(key, out var value))
                    result[key] = value;

            return result;
        }

        public static bool IsNullable(ITypeSymbol type)
        {
            if (type == null) return false;

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                    return true;
            }

            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        public static bool IsCollectionType(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.TypeKind == TypeKind.Array) return true;

            if (type is INamedTypeSymbol namedType)
            {
                return namedType.AllInterfaces.Any(i => i.ToDisplayString().Contains("IEnumerable")) ||
                       _collectionTypeNames.Any(name => namedType.ToDisplayString().Contains(name));
            }
            return false;
        }

        public static string GetProjectDirectory(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var msbuildProjectDir = ReadMsBuildProperty(options, "MSBuildProjectDirectory");
            if (!string.IsNullOrWhiteSpace(msbuildProjectDir)) return msbuildProjectDir;

            var msbuildProjectFile = ReadMsBuildProperty(options, "MSBuildProjectFile");
            if (!string.IsNullOrWhiteSpace(msbuildProjectFile))
            {
                var dir = Path.GetDirectoryName(msbuildProjectFile);
                if (!string.IsNullOrWhiteSpace(dir)) return dir;
            }

            var firstTree = compilation.SyntaxTrees.FirstOrDefault();
            if (firstTree != null && !string.IsNullOrWhiteSpace(firstTree.FilePath))
            {
                return Path.GetDirectoryName(firstTree.FilePath) ?? string.Empty;
            }

            return string.Empty;
        }

        public static (string Type, string SubDomain, bool Overwrite) ExtractGenerateAttributeParams(AttributeData attribute)
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));

            var type = GetNamedArgumentValue<string>(attribute, "Type") ??
                       GetConstructorArgumentValue<string>(attribute, 0);

            var subDomain = GetNamedArgumentValue<string>(attribute, "SubDomain") ??
                               GetConstructorArgumentValue<string>(attribute, 1) ?? "Default";

            var overwriteRaw = GetNamedArgumentValue<object>(attribute, "Overwrite");
            bool overwrite = (overwriteRaw is bool b) ? b : false;

            return (type, subDomain, overwrite);
        }

        private static T GetNamedArgumentValue<T>(AttributeData attribute, string argumentName)
        {
            var namedArgument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
            if (namedArgument.Value.Kind != TypedConstantKind.Error && namedArgument.Value.Value is T value)
                return value;
            return default(T);
        }

        private static T GetConstructorArgumentValue<T>(AttributeData attribute, int index)
        {
            if (index < 0 || index >= attribute.ConstructorArguments.Length) return default(T);
            if (attribute.ConstructorArguments[index].Value is T value) return value;
            return default(T);
        }

        public static string GetOutputPath(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var projectDir = GetProjectDirectory(options, compilation);
            var outputPath = ReadMsBuildProperty(options, "OutputPath") ?? "bin" + Path.DirectorySeparatorChar + "Debug";

            return Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(projectDir, outputPath);
        }

        public static string GetBuildConfiguration(AnalyzerConfigOptionsProvider options)
            => ReadMsBuildProperty(options, "Configuration") ?? "Debug";

        public static string GetTargetFramework(AnalyzerConfigOptionsProvider options)
            => ReadMsBuildProperty(options, "TargetFramework") ?? "net10.0";

        public static string GetRootNamespace(AnalyzerConfigOptionsProvider options, Compilation compilation)
            => ReadMsBuildProperty(options, "RootNamespace") ?? compilation.AssemblyName;

        public static void ValidateConfiguration(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var targetFramework = GetTargetFramework(options);
            if (string.IsNullOrWhiteSpace(targetFramework))
                throw new InvalidOperationException("未配置目标框架(TargetFramework)");

            var outputPath = GetOutputPath(options, compilation);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("输出路径(OutputPath)无效");
        }

        public static void LogConfiguration(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            Debug.WriteLine("=== 当前配置 ===");
            Debug.WriteLine("目标框架: " + GetTargetFramework(options));
            Debug.WriteLine("输出路径: " + GetOutputPath(options, compilation));
            Debug.WriteLine("根命名空间: " + (GetRootNamespace(options, compilation) ?? "未命名"));
            Debug.WriteLine("构建配置: " + GetBuildConfiguration(options));
            Debug.WriteLine("================");
        }

        public static INamedTypeSymbol GetClassSymbolWithDebug(Compilation compilation, string type)
        {
            var symbol = compilation.GetTypeByMetadataName(type);
            if (symbol == null) Debug.WriteLine("无法找到类型: " + type);
            return symbol;
        }

        public static string GetAssemblyName(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (compilation == null) throw new ArgumentNullException(nameof(compilation));

            var assemblyName = ReadMsBuildProperty(options, "AssemblyName");
            if (!string.IsNullOrEmpty(assemblyName)) return assemblyName;

            return compilation.AssemblyName;
        }

        public static string GetNullableConfig(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var config = ReadMsBuildProperty(options, "Nullable");
            if (!string.IsNullOrEmpty(config)) return config;

            if (compilation.Options is CSharpCompilationOptions csharpOptions)
            {
                switch (csharpOptions.NullableContextOptions)
                {
                    case NullableContextOptions.Enable: return "enable";
                    case NullableContextOptions.Warnings: return "warnings";
                    case NullableContextOptions.Annotations: return "annotations";
                    default: return "disable";
                }
            }
            return "disable";
        }

        public static string GetLangVersion(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var version = ReadMsBuildProperty(options, "LangVersion");
            if (!string.IsNullOrEmpty(version)) return version;

            if (compilation is CSharpCompilation csharp)
            {
                // C# 7.3 不支持简洁的 switch 表达式，使用 ToString() 处理
                string v = csharp.LanguageVersion.ToString();
                return v.Replace("CSharp", "").Replace("_", ".");
            }

            return "latest";
        }

        public static string GetGeneratedFilesDirectory(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            var path = ReadMsBuildProperty(options, "CompilerGeneratedFilesOutputPath");
            if (!string.IsNullOrEmpty(path)) return path;

            return Path.Combine(GetOutputPath(options, compilation), "Generated");
        }

        public static string GetGeneratedRootNamespace(string rootNamespace)
            => string.IsNullOrEmpty(rootNamespace) ? "Generated" : rootNamespace + ".Generated";
    }
}