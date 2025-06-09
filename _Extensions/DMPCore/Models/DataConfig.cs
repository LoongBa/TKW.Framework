namespace TKWF.DMPCore.Models;
/// <summary>
/// 数据配置
/// </summary>
public class DataConfig
{
    public string LoaderType { get; set; }
    public DataLoadOptions LoaderOptions { get; set; }
    public string[] DimensionFields { get; set; }
    public List<string> PreprocessorPlugins { get; set; }
}