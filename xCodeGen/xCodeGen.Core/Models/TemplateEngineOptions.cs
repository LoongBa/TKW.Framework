namespace xCodeGen.Core.Models;

/// <summary>
/// 模板引擎的专用配置
/// </summary>
public class TemplateEngineOptions
{
    /// <summary>
    /// 是否启用模板缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存过期时间（分钟）
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;
}