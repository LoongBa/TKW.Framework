namespace TKWF.DMP.Core.Models;

public class DataExportConfig
{
    public string ExportFormat { get; set; } = "Json";
    public string OutputPath { get; set; } = string.Empty;
}