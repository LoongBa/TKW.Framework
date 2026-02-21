using System;
using System.Collections.Generic;
using System.IO;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 项目配置信息容器，包含项目编译、路径、框架等关键信息
    /// </summary>
    public class ProjectConfiguration
    {
        /// <summary>
        /// 初始化项目配置信息
        /// </summary>
        /// <param name="rootNamespace">项目根命名空间（必填）</param>
        /// <param name="projectDirectory">项目文件所在目录（必填）</param>
        /// <param name="outputPath">编译输出目录（必填）</param>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="targetFramework">目标框架（如 net6.0）</param>
        /// <param name="buildConfiguration">编译配置（Debug/Release）</param>
        /// <param name="langVersion">C#语言版本</param>
        /// <param name="nullable">Nullable配置模式</param>
        /// <param name="generatedNamespace">生成代码的基础命名空间</param>
        /// <param name="generatedFilesDirectory">生成代码的输出路径</param>
        /// <param name="generatorVersion">代码生成器版本</param>
        public ProjectConfiguration(
            string rootNamespace,
            string projectDirectory,
            string outputPath,
            string assemblyName = null,
            string targetFramework = null,
            string buildConfiguration = null,
            string langVersion = null,
            string nullable = null,
            string generatedNamespace = null,
            string generatedFilesDirectory = null,
            string generatorVersion = null)
        {
            // 验证必填参数
            RootNamespace = rootNamespace ?? throw new ArgumentNullException(nameof(rootNamespace));
            ProjectDirectory = Path.GetFullPath(projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory)));
            OutputPath = string.IsNullOrEmpty(outputPath) ? string.Empty: Path.GetFullPath(outputPath);

            // 可选参数初始化
            AssemblyName = assemblyName ?? rootNamespace;
            TargetFramework = targetFramework ?? "net6.0";
            Configuration = buildConfiguration ?? "Debug";
            LangVersion = langVersion ?? "7.3";
            Nullable = nullable ?? "disable";
            GeneratorVersion = generatorVersion ?? "xCodeGen.Engine.V2";
            GeneratedFilesDirectory = generatedFilesDirectory;
            GeneratedNamespace = generatedNamespace ?? $"{rootNamespace}.Generated";

            // 初始化集合属性
            CustomProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FrameworkReferences = new List<string>();
            ProjectReferences = new List<string>();
        }

        #region 核心命名空间信息
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
        public string TargetFramework { get; internal set; }

        /// <summary>
        /// 编译配置类型（Debug/Release）
        /// </summary>
        public string Configuration { get; internal set; }

        /// <summary>
        /// C#语言版本（如 7.3、10.0）
        /// </summary>
        public string LangVersion { get; internal set; }

        /// <summary>
        /// Nullable引用类型配置（disable/enable/warnings/annotations）
        /// </summary>
        public string Nullable { get; internal set; }

        /// <summary>
        /// 代码生成器版本
        /// </summary>
        public string GeneratorVersion { get; internal set; }

        /// <summary>
        /// 项目文件所在目录（绝对路径）
        /// </summary>
        public string ProjectDirectory { get; }

        /// <summary>
        /// 编译输出目录（绝对路径）
        /// </summary>
        public string OutputPath { get; internal set; }

        /// <summary>
        /// 生成代码存放目录（绝对路径，默认为项目目录下的Generated文件夹）
        /// </summary>
        public string GeneratedFilesDirectory { get; }

        #endregion

        #region 引用信息
        /// <summary>
        /// 项目引用的框架列表（如 Microsoft.NET.Sdk.Web）
        /// </summary>
        public List<string> FrameworkReferences { get; }

        /// <summary>
        /// 项目引用的其他项目列表（项目路径）
        /// </summary>
        public List<string> ProjectReferences { get; }

        /// <summary>
        /// 自定义属性字典（用于存储额外配置）
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 转换为字符串表示（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"{RootNamespace} ({TargetFramework}) - {Configuration}";
        }
        #endregion
    }
}
