using System.Text.Json.Serialization;

namespace TKWF.DMP.Core.Models;

/// <summary>
/// 配置类，用于定义指标计算器的配置
/// </summary>
public class MetricConfig
{
    public string Name { get; set; } = string.Empty;
    public string CalculatorType { get; set; } = string.Empty;

    [JsonConverter(typeof(PropertyMapConverter))]
    public Dictionary<string, string> PropertyMap { get; set; } = [];

    // 预处理器名称列表
    public List<string> PreprocessorNames { get; set; } = [];
}