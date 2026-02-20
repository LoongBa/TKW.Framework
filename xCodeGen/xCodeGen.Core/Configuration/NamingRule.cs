namespace xCodeGen.Core.Configuration;

/// <summary>
/// 命名规则
/// </summary>
public class NamingRule
{
    /// <summary>
    /// 产物类型
    /// </summary>
    public string ArtifactType { get; set; }
        
    /// <summary>
    /// 命名模式，可用占位符: {ClassName} - 原始名称, {ArtifactType} - 产物类型
    /// </summary>
    public string Pattern { get; set; } = "{ClassName}{ArtifactType}";
}