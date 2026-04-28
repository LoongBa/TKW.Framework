using System;
using System.IO;
using xCodeGen.Core.Configuration;

namespace xCodeGen.Core.Services;

/// <summary>
/// 负责加载、验证和持久化 xCodeGen.json
/// </summary>
public class ConfigurationProvider
{
    public const string ConfigFileName = "xCodeGen.json";

    /// <summary>
    /// 从指定目录加载配置文件
    /// </summary>
    public CodeGenConfig Load(string? searchDirectory = null, string? explicitPath = null)
    {
        string finalPath;

        // 1. 确定配置文件的绝对路径
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            // 如果指定了 -j，直接定位到该文件
            finalPath = Path.GetFullPath(explicitPath);
        }
        else
        {
            // 否则在搜索目录下找默认文件
            searchDirectory ??= Directory.GetCurrentDirectory();
            finalPath = Path.Combine(searchDirectory, ConfigFileName);
        }

        if (!File.Exists(finalPath))
            throw new FileNotFoundException($"找不到配置文件: {finalPath}");

        // 2. 加载内容
        var json = File.ReadAllText(finalPath);
        var config = CodeGenConfig.FromJson(json);

        // 3. 处理路径绝对化
        // 基础参考点是配置文件所在的文件夹
        var referenceDir = Path.GetDirectoryName(finalPath) ?? AppContext.BaseDirectory;

        if (!Path.IsPathRooted(config.OutputRoot))
            config.OutputRoot = Path.GetFullPath(Path.Combine(referenceDir, config.OutputRoot));

        if (!Path.IsPathRooted(config.TemplatesPath))
            config.TemplatesPath = Path.GetFullPath(Path.Combine(referenceDir, config.TemplatesPath));

        // 针对 TargetProject (csproj) 也要处理，方便后续 Roslyn 分析
        if (!string.IsNullOrEmpty(config.TargetProject) && !Path.IsPathRooted(config.TargetProject))
            config.TargetProject = Path.GetFullPath(Path.Combine(referenceDir, config.TargetProject));

        return config;
    }

    private CodeGenConfig NormalizePaths(CodeGenConfig config, string baseDir)
    {
        // 确保输出路径等基于配置文件所在目录进行转换
        if (!Path.IsPathRooted(config.OutputRoot))
            config.OutputRoot = Path.GetFullPath(Path.Combine(baseDir, config.OutputRoot));

        return config;
    }
}