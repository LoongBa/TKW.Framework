using System;
using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Core.Models;

/// <summary>
/// 代码生成的全局配置选项
/// </summary>
public class GenerateOptions
{
    /// <summary>
    /// 启用的元数据来源（字符串集合，与MetadataSource枚举对应）
    /// </summary>
    public IReadOnlyList<string> MetadataSources { get; set; } = (List<string>) [];

    /// <summary>
    /// 项目根路径
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 生成代码的输出路径
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 元数据文件的输出路径（如.Meta.cs文件）
    /// </summary>
    public string MetaOutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 模板文件所在路径
    /// </summary>
    public string TemplatePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件名格式化模板（含{ClassName}占位符）
    /// </summary>
    public string FileNameFormat { get; set; } = "{ClassName}.generated.cs";

    /// <summary>
    /// 是否覆盖已有文件
    /// </summary>
    public bool Overwrite { get; set; } = true;

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool EnableDetailedLogging { get; set; }

    /// <summary>
    /// 是否允许项目路径为空（适用于无项目文件的场景）
    /// </summary>
    public bool AllowEmptyProjectPath { get; set; } = false;

    /// <summary>
    /// 提取器超时时间（毫秒）
    /// </summary>
    public int ExtractorTimeoutMs { get; set; } = 30000; // 默认30秒

    /// <summary>
    /// 提取器专属配置（key: MetadataSource字符串值）
    /// </summary>
    public Dictionary<string, string> ExtractorConfigs { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = [];

        if (!AllowEmptyProjectPath && string.IsNullOrWhiteSpace(ProjectPath))
            errors.Add("ProjectPath 不能为空（如需允许空路径，请设置AllowEmptyProjectPath=true）");

        if (string.IsNullOrWhiteSpace(OutputPath))
            errors.Add("OutputPath 不能为空");

        if (string.IsNullOrWhiteSpace(TemplatePath))
            errors.Add("TemplatePath 不能为空");

        if (MetadataSources == null || !MetadataSources.Any())
            errors.Add("至少需要指定一个 MetadataSources");

        return errors.Count == 0;
    }
}
