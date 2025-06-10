namespace TKWF.DMP.Core.Models;

public class DataLoadingConfig
{
    public string DataSourceType { get; set; } = "InMemory";
    public Dictionary<string, string> ConnectionParameters { get; set; } = new();
}