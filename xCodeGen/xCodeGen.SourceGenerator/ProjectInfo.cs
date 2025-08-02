using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core;
using xCodeGen.Core.Utilities;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 项目信息提取器，统一封装项目级配置信息的获取逻辑
    /// </summary>
    public class ProjectInfo
    {
        public AnalyzerConfigOptionsProvider Options { get; }
        public Compilation Compilation { get; }

        /// <summary>
        /// 项目根命名空间（来自MSBuild的RootNamespace）
        /// </summary>
        public string RootNamespace { get; }

        /// <summary>
        /// 生成代码的基础命名空间
        /// </summary>
        public string GeneratedNamespace { get; }

        /// <summary>
        /// 程序集名称（来自MSBuild的AssemblyName）
        /// </summary>
        public string AssemblyName { get; }
        /// <summary>
        /// 目标框架版本（如 net6.0、net7.0）
        /// </summary>
        public string TargetFramework { get; }

        /// <summary>
        /// 编译配置类型（Debug/Release）
        /// </summary>
        public string BuildConfiguration { get; }

        /// <summary>
        /// C#语言版本（如 7.3、10.0）
        /// </summary>
        public string LangVersion { get; }

        /// <summary>
        /// Nullable引用类型配置（disable/enable/warnings/annotations）
        /// </summary>
        public string Nullable { get; }
        /// <summary>
        /// 代码生成器版本
        /// </summary>
        public string GeneratorVersion { get; }

        /// <summary>
        /// 项目文件所在目录（绝对路径）
        /// </summary>
        public string ProjectDirectory { get; }

        /// <summary>
        /// 编译输出目录（绝对路径）
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// 获取生成文件的最终存放路径（绝对路径）
        /// <remarks>优先取项目配置 "CompilerGeneratedFilesOutputPath" 的值</remarks>
        /// <remarks>其次，默认为项目输出目录下的 Generated 文件夹</remarks>
        /// </summary>
        public string GeneratedFilesDirectory { get; }

        public ProjectInfo(AnalyzerConfigOptionsProvider options, Compilation compilation)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));

            // 生成器版本，源生成器是否支持从当前 Assembly 获取？
            GeneratorVersion = "xCodegen.SourceGenerator.V2";
            // 项目目录
            ProjectDirectory = CodeAnalysisHelper.GetProjectDirectory(Options, Compilation);
            // 项目生成路径
            OutputPath = CodeAnalysisHelper.GetOutputPath(Options, Compilation);
            // 根命名空间获取逻辑
            RootNamespace = CodeAnalysisHelper.GetRootNamespace(Options, Compilation);
            // 程序集名称
            AssemblyName = CodeAnalysisHelper.GetAssemblyName(Options, Compilation);
            // 目标框架
            TargetFramework = CodeAnalysisHelper.GetTargetFramework(Options);
            // 编译配置
            BuildConfiguration = CodeAnalysisHelper.GetBuildConfiguration(Options);
            // C#语言版本
            LangVersion = CodeAnalysisHelper.GetLangVersion(Options, Compilation);
            // Nullable配置
            Nullable = CodeAnalysisHelper.GetNullableConfig(Options, Compilation);
            // 生成代码存放目录
            GeneratedFilesDirectory = CodeAnalysisHelper.GetGeneratedFilesDirectory(Options, Compilation);
            // 生成代码的基础命名空间
            GeneratedNamespace = CodeAnalysisHelper.GetGeneratedRootNamespace(RootNamespace);
        }

        /// <summary>
        /// 创建项目配置信息
        /// </summary>
        /// <returns>统一的项目配置</returns>
        public ProjectConfiguration CreateProjectConfiguration()
        {
            return new ProjectConfiguration(
                rootNamespace: RootNamespace,
                projectDirectory: ProjectDirectory,
                outputPath: OutputPath,
                assemblyName: AssemblyName,
                targetFramework: TargetFramework,
                buildConfiguration: BuildConfiguration,
                langVersion: LangVersion,
                nullable: Nullable,
                generatedNamespace: GeneratedNamespace,
                generatedFilesDirectory: GeneratedFilesDirectory,
                generatorVersion: GeneratorVersion
            );
        }
    }
}