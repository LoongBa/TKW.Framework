using Microsoft.AspNetCore.Http;

namespace TKW.Framework.Domain.Web;

public class DomainSessionOptions
{
    // Cookie 相关设置
    public string CookieName { get; set; } = "SessionKey";
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(30);
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;
    public bool HttpOnly { get; set; } = true;

    // 参数名称设置（支持从不同来源提取）
    public string HeaderName { get; set; } = "X-Session-Key";
    public string QueryName { get; set; } = "sk";
    public string FormName { get; set; } = "sessionKey";
}