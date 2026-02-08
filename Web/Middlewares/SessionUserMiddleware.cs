#nullable enable
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Web.Users;

namespace TKW.Framework.Web.Middlewares;

/// <summary>
/// 从请求中提取 SessionKey，加载 DomainUser 并注入 HttpContext
/// 支持 WebAPI、GraphQL、FastEndpoint、Blazor Server
/// </summary>
public class SessionUserMiddleware<TUserInfo>(RequestDelegate next, ISessionManager<TUserInfo> sessionManager)
    where TUserInfo : class, IUserInfo, new()
{
    public const string KeyName_DomainUser = "DomainUser";
    public const string SessionKeyName_Cookie = "SessionKey";
    public const string SessionKeyName_Header = "X-Session-Key";
    public const string SessionKeyName_Query = "sk";
    public const string SessionKeyName_Form = "sessionKey"; // 新增：支持 Form 提交

    public async Task InvokeAsync(HttpContext context)
    {
        var sessionKey = GetSessionKeyFromRequest(context);
        DomainUser<TUserInfo>? currentUser = null;

        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            try
            {
                var session = await sessionManager.GetSessionAsync(sessionKey);
                currentUser = session?.User;
            }
            catch { /* 日志 */ }
        }

        // fallback: 无 SessionKey 或失效 → 创建 Guest
        if (currentUser == null)
        {
            var guestSession = await DomainHost<TUserInfo>.Root!.NewGuestSessionAsync();  // 调用 DomainHost 创建 Guest
            currentUser = guestSession.User;

            // 保存新 SessionKey 到响应（优先 Cookie，备用 Header）
            context.Response.Cookies.Append(SessionKeyName_Cookie, guestSession.Key, new()
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromMinutes(30)  // 短 TTL
            });
            context.Response.Headers.Add(SessionKeyName_Header, guestSession.Key);  // 备用：Header 形式返回 SessionKey，方便 API 客户端使用
        }

        // 注入 ClaimsPrincipal + Items
        if (currentUser != null)
        {
            context.User = currentUser.ToNewClaimsPrincipal();
            context.Items[KeyName_DomainUser] = currentUser;
        }

        await next(context);
    }

    private static string? GetSessionKeyFromRequest(HttpContext context)
    {
        // Header（推荐 API 场景）
        if (context.Request.Headers.TryGetValue(SessionKeyName_Header, out var headerValues) && headerValues.Count > 0)
            return headerValues[0];

        // Cookie（推荐浏览器场景）
        if (context.Request.Cookies.TryGetValue(SessionKeyName_Cookie, out var cookieValue))
            return cookieValue;

        // QueryString（调试/小程序 H5 场景）
        if (context.Request.Query.TryGetValue(SessionKeyName_Query, out var queryValues) && queryValues.Count > 0)
            return queryValues[0];

        // Form（支持 POST Form 提交场景，如 Blazor 表单）
        if (context.Request.Method == HttpMethods.Post &&
            context.Request.Form.TryGetValue(SessionKeyName_Form, out var formValues) && formValues.Count > 0)
            return formValues[0];

        return null;
    }
}