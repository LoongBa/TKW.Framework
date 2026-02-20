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
    public CodeGenConfig Load(string searchDirectory)
    {
        var configPath = Path.Combine(searchDirectory, ConfigFileName);

        if (!File.Exists(configPath))
        {
            // 如果不存在，返回默认配置或抛出异常
            return new CodeGenConfig();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = CodeGenConfig.FromJson(json);

            // 可以在此处处理路径的相对转绝对逻辑
            return NormalizePaths(config, searchDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载配置文件失败: {ex.Message}");
        }
    }

    private CodeGenConfig NormalizePaths(CodeGenConfig config, string baseDir)
    {
        // 确保输出路径等基于配置文件所在目录进行转换
        if (!Path.IsPathRooted(config.OutputRoot))
            config.OutputRoot = Path.GetFullPath(Path.Combine(baseDir, config.OutputRoot));

        return config;
    }
}