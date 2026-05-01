namespace xCodeGen.Core.Models;

/// <summary>
/// 代码生成的全局配置选项
/// </summary>
public class GenerateOptions
{
    /// <summary>
    /// 项目根路径
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 生成代码的输出路径
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool EnableDetailedLogging { get; set; }
}
