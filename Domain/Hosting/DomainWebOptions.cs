using TKW.Framework.Common.Attributes;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// Web 专用领域配置扩展
/// </summary>
[DomainMapFrom]
public class DomainWebOptions: DomainOptions
{
    public bool UseWebExceptionMiddleware { get; set; } = true;
    /// <summary>
    /// 是否自动注入 IHttpContextAccessor。默认为 true。
    /// 许多领域服务（如获取当前会话用户）依赖于此服务。
    /// </summary>
    public bool AutoAddHttpContextAccessor { get; set; } = true;
    public bool SuppressRoutingWarning { get; set; } = false;
    public bool HasRoutingPhase { get; set; } = false;
    public WebSessionOptions WebSession { get; set; } = new();
}