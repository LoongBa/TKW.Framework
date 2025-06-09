namespace TKWF.DMP.Core.Models;

/// <summary>
/// 指标规则
/// </summary>
public class MetricRule
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Field { get; set; }
    public string Alias { get; set; }
    public string Unit { get; set; }
}