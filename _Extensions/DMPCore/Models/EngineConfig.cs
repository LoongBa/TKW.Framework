namespace TKWF.DMP.Core.Models;

/// <summary>
/// 引擎级统一配置（整合所有阶段配置 + 动态加载策略）
/// </summary>
public class EngineConfig
{
    public DataLoadingConfig DataLoading { get; set; } = new();
    public ProcessingConfig Processing { get; set; } = new();
    public DataExportConfig Export { get; set; } = new();
    public DynamicLoadingConfig DynamicLoading { get; set; } = new()
    {
        EnableDynamicLoading = false,
        ScanAssemblies = true,
        AssemblyPaths = [],
        ComponentNameToTypeMap = new Dictionary<string, string>()
    };
}