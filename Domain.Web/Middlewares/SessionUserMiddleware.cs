using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Web.Middlewares;

/// <summary>
/// SessionUser 中间件 - 从请求中提取 SessionKey，加载 DomainUser 并注入 HttpContext
/// 支持 WebAPI、Blazor Server、Minimal API 等
/// </summary>
public class SessionUserMiddleware<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public const string KeyName_DomainUser = "DomainUser";
    private readonly RequestDelegate _next;
    private readonly ISessionManager<TUserInfo> _SessionManager;
    private readonly ILogger<SessionUserMiddleware<TUserInfo>> _Logger;
    private readonly DomainHost<TUserInfo> _DomainHost;

    /// <summary>
    /// SessionUser 中间件 - 从请求中提取 SessionKey，加载 DomainUser 并注入 HttpContext
    /// 支持 WebAPI、Blazor Server、Minimal API 等
    /// </summary>
    public SessionUserMiddleware(RequestDelegate next,
        DomainHost<TUserInfo> domainHost, ISessionManager<TUserInfo> sessionManager)
    {
        _DomainHost = domainHost.EnsureNotNull();
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _Logger = domainHost.LoggerFactory.CreateLogger<SessionUserMiddleware<TUserInfo>>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var sessionKey = GetSessionKeyFromRequest(context);
            DomainUser<TUserInfo>? domainUser = null;

            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                domainUser = await TryLoadUserAsync(sessionKey);
            }

            // 无有效会话 → 创建游客会话
            if (domainUser == null)
            {
                var guestSession = await _DomainHost.NewGuestSessionAsync();
                domainUser = guestSession.User!;

                SetSessionKeyToResponse(context, guestSession.Key);
                _Logger.LogInformation("创建新游客会话 - SessionKey: {SessionKey}", guestSession.Key);
            }

            // 注入到 HttpContext（供 Controller、Endpoint、Blazor 使用）
            context.Items[KeyName_DomainUser] = domainUser;
            context.User = domainUser.ToClaimsPrincipal();   // 假设你有这个扩展方法

            await _next(context);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "SessionUserMiddleware 执行异常");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("会话处理失败");
        }
    }

    private async Task<DomainUser<TUserInfo>?> TryLoadUserAsync(string sessionKey)
    {
        try
        {
            var session = await _SessionManager.GetAndActiveSessionAsync(sessionKey);
            return session.User;
        }
        catch (Exception ex)
        {
            _Logger.LogWarning(ex, "加载会话失败 - SessionKey: {SessionKey}", sessionKey);
            return null;
        }
    }

    private static string? GetSessionKeyFromRequest(HttpContext context)
    {
        // 优先级：Header > Cookie > Query > Form
        if (context.Request.Headers.TryGetValue("X-Session-Key", out var header) && header.Count > 0)
            return header[0];

        if (context.Request.Cookies.TryGetValue("SessionKey", out var cookie))
            return cookie;

        if (context.Request.Query.TryGetValue("sk", out var query) && query.Count > 0)
            return query[0];

        if (context.Request.Method == HttpMethods.Post &&
            context.Request.Form.TryGetValue("sessionKey", out var form) && form.Count > 0)
            return form[0];

        return null;
    }

    private static void SetSessionKeyToResponse(HttpContext context, string sessionKey)
    {
        context.Response.Cookies.Append("SessionKey", sessionKey, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(30)
        });

        context.Response.Headers.Append("X-Session-Key", sessionKey);
    }
}