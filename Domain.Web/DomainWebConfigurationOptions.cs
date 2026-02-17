namespace TKW.Framework.Domain.Web;

/// <summary>
/// Web 专用领域配置扩展
/// </summary>
public class DomainWebConfigurationOptions : DomainOptions
{
    public bool UseDomainExceptionMiddleware { get; set; } = true;
    /// <summary>
    /// 是否自动注入 IHttpContextAccessor。默认为 true。
    /// 许多领域服务（如获取当前会话用户）依赖于此服务。
    /// </summary>
    public bool AutoAddHttpContextAccessor { get; set; } = true;
    public bool SuppressRoutingWarning { get; set; } = false;
    internal bool HasRoutingPhase { get; set; } = false;
    public WebSessionOptions WebSession { get; set; } = new();
}