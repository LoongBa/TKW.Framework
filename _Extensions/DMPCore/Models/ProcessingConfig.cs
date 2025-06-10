namespace TKWF.DMP.Core.Models;

public class ProcessingConfig
{
    public List<string> PreprocessorNames { get; set; } = new();
    public MetricConfig MetricConfig { get; set; } = new();
}