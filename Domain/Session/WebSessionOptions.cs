using System;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Session;

public class WebSessionOptions : DomainSessionOptions
{
    // Cookie 相关设置
    public string CookieName => SessionKeyName;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(30);
    //TODO: public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;
    public bool HttpOnly { get; set; } = true;

    // 参数名称设置（支持从不同来源提取）
    public string HeaderName { get; set; } = "X-Session-Key";
    public string QueryName { get; set; } = "sk";
    public string FormName { get; set; } = "sessionKey";
}