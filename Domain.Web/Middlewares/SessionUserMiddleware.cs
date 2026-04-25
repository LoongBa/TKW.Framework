using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Session;

namespace TKW.Framework.Domain.Web.Middlewares;

/// <summary>
/// SessionUser 中间件 - 从请求中提取 SessionKey，加载 DomainUser 并注入 HttpContext
/// 支持 WebAPI、Blazor Server、Minimal API 等
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public class SessionUserMiddleware<TUserInfo>(
    RequestDelegate next,
    DomainHost<TUserInfo> domainHost,
    WebSessionOptions options)
    where TUserInfo : class, IUserInfo, new()
{
    // 使用配置选项类，替代了硬编码的常量，提高了灵活性
    private readonly ILogger<SessionUserMiddleware<TUserInfo>> _Logger 
        = domainHost.LoggerFactory.CreateLogger<SessionUserMiddleware<TUserInfo>>();

    public const string ContextKeyName_DomainUser = "DomainUser";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var sessionKey = GetSessionKeyFromRequest(context);

            // 启用领域会话范围，自动加载用户信息：传入 context.RequestServices 重用当前容器
            await using var scope = await domainHost.BeginSessionScopeAsync(context.RequestServices, sessionKey);
            var user = scope.User;
            
            if (sessionKey != user.SessionKey)          // 如果生成了新游客，回写 Key
                SetSessionKeyToResponse(context, user.SessionKey);
            
            context.Items["DomainUser"] = user;         // 写入上下文
            context.User = user.ToClaimsPrincipal();    // 兼容 ASP.NET Core 的授权机制

            await next(context);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "领域层执行异常");
            throw;
        }
    }

    /// <summary>
    /// 从请求中提取 SessionKey
    /// 优先级：Header > Cookie > Query > Form
    /// </summary>
    private string? GetSessionKeyFromRequest(HttpContext context)
    {
        // 1. 检查 Header
        if (context.Request.Headers.TryGetValue(options.HeaderName, out var header)) return header[0];

        // 2. 检查 Cookie
        if (context.Request.Cookies.TryGetValue(options.CookieName, out var cookie)) return cookie;

        // 3. 检查 Query String
        if (context.Request.Query.TryGetValue(options.QueryName, out var query)) return query[0];

        // 4. 检查 Form (关键修复点)
        // 必须先判断 Method 是否为 POST (或其他允许 Body 的方法)
        // 并且必须判断 HasFormContentType，防止在非表单请求（如 GET 或 JSON POST）中访问 Form 属性导致 InvalidOperationException
        if (context.Request.Method == HttpMethods.Post && context.Request.HasFormContentType)
            if (context.Request.Form.TryGetValue(options.FormName, out var form)) return form[0];

        return null;
    }

    /// <summary>
    /// 将 SessionKey 设置到响应中
    /// </summary>
    private void SetSessionKeyToResponse(HttpContext context, string sessionKey)
    {
        // 设置 Cookie，参数从 Options 中读取，便于统一管理
        context.Response.Cookies.Append(options.CookieName, sessionKey, new CookieOptions
        {
            HttpOnly = options.HttpOnly,
            Secure = context.Request.IsHttps,
            SameSite = options.SameSite,
            MaxAge = options.ExpiredTimeSpan
        });

        // 同时也写入 Header，方便非浏览器客户端（如 App、小程序）获取
        context.Response.Headers.Append(options.HeaderName, sessionKey);
    }
}
