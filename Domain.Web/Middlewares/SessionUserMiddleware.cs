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
    public const string ContextKeyName_DomainUser = "DomainUser";
    public const string SessionKeyName_Cookie = "SessionKey";
    public const string SessionKeyName_Header = "X-Session-Key";
    public const string SessionKeyName_Query = "sk";
    public const string SessionKeyName_Form = "sessionKey"; // 新增：支持 Form 提交

    private readonly RequestDelegate _Next;
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
        _Next = next ?? throw new ArgumentNullException(nameof(next));
        _SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _Logger = domainHost.LoggerFactory.CreateLogger<SessionUserMiddleware<TUserInfo>>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var sessionKey = GetSessionKeyFromRequest(context);
            DomainUser<TUserInfo>? domainUser;
            SessionInfo<TUserInfo>? session = null;

            if (!string.IsNullOrWhiteSpace(sessionKey))
                session = await _SessionManager.GetAndActiveSessionAsync(sessionKey);

            // 无有效会话 → 创建游客会话
            if (session == null)
            {
                session = await _DomainHost.NewGuestSessionAsync();
                domainUser = session.User!;

                SetSessionKeyToResponse(context, session.Key);
                _Logger.LogInformation("创建新游客会话 - SessionKey: {SessionKey}", session.Key);
            }
            else
                domainUser = session.User!;

            // 注入到 HttpContext（供 Controller、Endpoint、Blazor 使用）
            context.Items[ContextKeyName_DomainUser] = domainUser;
            context.User = domainUser.ToClaimsPrincipal();   // 假设你有这个扩展方法

            await _Next(context);
        }
        catch (Exception ex)
        {
            var message = $"SessionUserMiddleware 执行异常：{ex.Message}";
            _Logger.LogError(ex, message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync(message);
        }
    }

    private static string? GetSessionKeyFromRequest(HttpContext context)
    {
        // 优先级：Header > Cookie > Query > Form
        if (context.Request.Headers.TryGetValue(SessionKeyName_Header, out var header) && header.Count > 0)
            return header[0];

        if (context.Request.Cookies.TryGetValue(SessionKeyName_Cookie, out var cookie))
            return cookie;

        if (context.Request.Query.TryGetValue(SessionKeyName_Query, out var query) && query.Count > 0)
            return query[0];

        if (context.Request.Method == HttpMethods.Post && context.Request.HasFormContentType)
            if (context.Request.Form.TryGetValue(SessionKeyName_Form, out var form) && form.Count > 0)
                return form[0];

        return null;
    }

    private static void SetSessionKeyToResponse(HttpContext context, string sessionKey)
    {
        context.Response.Cookies.Append(SessionKeyName_Cookie, sessionKey, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(30)
        });

        context.Response.Headers.Append(SessionKeyName_Header, sessionKey);
    }
}