using System;
using System.IO;
using xCodeGen.Core.Configuration;

namespace xCodeGen.Core.Services;

/// <summary>
/// 负责加载、验证和持久化 xCodeGen.config.json
/// </summary>
public class ConfigurationProvider
{
    private const string ConfigFileName = "xCodeGen.config.json";

    /// <summary>
    /// 从指定目录加载配置文件
    /// </summary>
    public CodeGenConfig Load(string searchDirectory = null)
    {
        searchDirectory ??= AppContext.BaseDirectory;
        var configPath = Path.Combine(searchDirectory, ConfigFileName);

        if (!File.Exists(configPath))
            throw new FileNotFoundException($"找不到配置文件: {configPath}");

        var json = File.ReadAllText(configPath);
        var config = CodeGenConfig.FromJson(json);

        // 1. 处理基础目录绝对化
        if (!Path.IsPathRooted(config.OutputRoot))
            config.OutputRoot = Path.GetFullPath(Path.Combine(searchDirectory, config.OutputRoot));

        if (!Path.IsPathRooted(config.TemplatesPath))
            config.TemplatesPath = Path.GetFullPath(Path.Combine(searchDirectory, config.TemplatesPath));

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