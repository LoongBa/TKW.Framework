namespace TKWF.DMP.Core.Models;
/// <summary>
/// 配置基类
/// </summary>
public class StatConfig
{
    public TimeConfig TimeConfig { get; set; }
    public DataConfig DataConfig { get; set; }
    public List<MetricRule> MetricRules { get; set; }
    public List<string> CustomMetrics { get; set; }
    public PluginConfig PluginConfig { get; set; }
    public ValidationConfig Validation { get; set; }
    public PrivacyConfig Privacy { get; set; }
}