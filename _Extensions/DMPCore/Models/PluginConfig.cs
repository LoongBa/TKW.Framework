namespace TKWF.DMP.Core.Models;
/// <summary>
/// 插件配置
/// </summary>
public class PluginConfig
{
    public string LoadMode { get; set; }
    public string[] DebugAssemblies { get; set; }
    public List<string> ExporterPlugins { get; set; }
}