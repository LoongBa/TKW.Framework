using System;
using System.IO;
using xCodeGen.Core.Configuration;

namespace xCodeGen.Core.Services;

/// <summary>
/// 负责加载、验证和路径归一化 xCodeGen.json
/// </summary>
public class ConfigurationProvider
{
    public const string ConfigFileName = "xCodeGen.json";

    /// <summary>
    /// 从指定目录加载配置文件并完成路径初始化
    /// </summary>
    public CodeGenConfig Load(string? searchDirectory = null, string? explicitPath = null)
    {
        string finalPath;

        // 1. 确定配置文件的绝对路径
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            finalPath = Path.GetFullPath(explicitPath);
        }
        else
        {
            searchDirectory ??= Directory.GetCurrentDirectory();
            finalPath = Path.Combine(searchDirectory, ConfigFileName);
        }

        if (!File.Exists(finalPath))
            throw new FileNotFoundException($"找不到配置文件: {finalPath}");

        // 2. 加载内容
        var json = File.ReadAllText(finalPath);
        var config = CodeGenConfig.FromJson(json);

        // 3. 路径归一化
        var referenceDir = Path.GetDirectoryName(finalPath) ?? AppContext.BaseDirectory;
        return NormalizePaths(config, referenceDir);
    }

    /// <summary>
    /// 统一处理配置中的相对路径，转换为绝对路径
    /// </summary>
    private CodeGenConfig NormalizePaths(CodeGenConfig config, string baseDir)
    {
        // A. 基础路径：相对于配置文件所在目录
        config.OutputRoot = RootPath(config.OutputRoot, baseDir);
        config.TemplatesPath = RootPath(config.TemplatesPath, baseDir);
        config.TargetProject = RootPath(config.TargetProject, baseDir);

        // B. 业务接口合成路径：相对于 OutputRoot
        config.InterfaceOutputPath = RootPath(config.InterfaceOutputPath, config.OutputRoot);
        config.ServiceDirectory = RootPath(config.ServiceDirectory, config.OutputRoot);

        // C. 处理 Artifacts 字典中的深层路径
        foreach (var art in config.Artifacts.Values)
        {
            // 标准产物输出目录：如果为空则默认为 OutputRoot
            art.OutputDir = string.IsNullOrWhiteSpace(art.OutputDir) 
                ? config.OutputRoot : RootPath(art.OutputDir, config.OutputRoot);

            // 骨架产物目录：如果未设置则回退到 OutputDir
            art.SkeletonDir = string.IsNullOrWhiteSpace(art.SkeletonDir) 
                ? art.OutputDir : RootPath(art.SkeletonDir, config.OutputRoot);
        }

        return config;
    }

    /// <summary>
    /// 路径转换辅助工具
    /// </summary>
    private string RootPath(string path, string baseDir)
    {
        if (string.IsNullOrEmpty(path)) return baseDir;
        // 如果已经是绝对路径则直接返回，否则基于 baseDir 合并
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDir, path));
    }
}