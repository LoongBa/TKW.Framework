using System.Text.Json;
using TKWF.DMPCore.Interfaces;
using TKWF.DMPCore.Models;
using TKWF.DMPCore.Plugins.Converters;

namespace TKWF.DMPCore;

// 配置管理器实现
public class DefaultConfigManager : IConfigManager
{
    private readonly StatConfig _config;

    public DefaultConfigManager(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"配置文件不存在: {configPath}");

        var json = File.ReadAllText(configPath);
        _config = JsonSerializer.Deserialize<StatConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new DateTimeConverter() },
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        Validate();
    }

    public T GetConfig<T>(string section)
    {
        var property = typeof(StatConfig).GetProperty(section);
        if (property == null)
            throw new ArgumentException($"配置节 {section} 不存在");

        return (T)property.GetValue(_config);
    }

    public void Validate()
    {
        if (_config.TimeConfig == null)
            throw new InvalidOperationException("时间配置不能为空");

        if (_config.DataConfig == null)
            throw new InvalidOperationException("数据配置不能为空");

        if (_config.PluginConfig == null)
            throw new InvalidOperationException("插件配置不能为空");

        if (_config.MetricRules == null || !_config.MetricRules.Any())
            throw new InvalidOperationException("至少需要定义一个指标规则");
    }
}

// 

// 

// 

// 

// 

// 指标计算插件示例 - 复购率计算

// 结果输出插件示例 - 数据库输出

// 统计引擎实现
