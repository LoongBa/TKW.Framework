namespace TKWF.DMP.Core.Models;

public class DynamicLoadingConfig
{
    public bool EnableDynamicLoading { get; set; }
    public bool ScanAssemblies { get; set; }
    public IEnumerable<string> AssemblyPaths { get; set; } = new List<string>();
    public Dictionary<string, string> ComponentNameToTypeMap { get; set; } = new Dictionary<string, string>();
}