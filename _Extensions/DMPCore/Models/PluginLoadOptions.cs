namespace TKWF.DMP.Core.Models;
/// <summary>
/// 插件加载选项
/// </summary>

public record PluginLoadOptions(
    string LoadMode,
    string[]? DllPaths,
    string[] DebugAssemblies,
    string[] ExporterPaths
);