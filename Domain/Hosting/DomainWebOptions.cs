using TKW.Framework.Attributes;
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
    /// <summary>系统初始化异常时的重定向路径，默认为 "/Setup"</summary>
    /// <remarks>如果 AutoRedirectToSetup 为 true，则在发生未处理的初始化异常时，
    /// 系统会自动重定向到此路径，以便用户可以访问设置界面进行修复。</remarks>
    public string SetupPath { get; set; } = "/Setup";
    /// <summary>是否在发生未处理的初始化异常时自动重定向到 SetupPath 指定的路径。默认为 true</summary>
    public bool AutoRedirectToSetup { get; set; } = true;
}