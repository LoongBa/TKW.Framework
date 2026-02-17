using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Web.Middlewares;

/// <summary>
/// SessionUser 中间件 - 从请求中提取 SessionKey，加载 DomainUser 并注入 HttpContext
/// 支持 WebAPI、Blazor Server、Minimal API 等
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public class SessionUserMiddleware<TUserInfo>(
    RequestDelegate next,
    DomainHost<TUserInfo> domainHost,
    ISessionManager<TUserInfo> sessionManager,
    DomainSessionOptions options)
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
            // 1. 尝试从请求中获取 SessionKey
            var sessionKey = GetSessionKeyFromRequest(context);
            SessionInfo<TUserInfo>? session = null;

            // 2. 如果存在 Key，尝试从缓存/数据库加载并激活会话
            if (!string.IsNullOrWhiteSpace(sessionKey))
                session = await sessionManager.TryGetAndActiveSessionAsync(sessionKey);

            // 3. 如果会话无效，创建新的游客会话
            if (session == null)
            {
                session = await domainHost.NewGuestSessionAsync();
                // 将新的 SessionKey 写入响应（Cookie 和 Header）
                SetSessionKeyToResponse(context, session.Key);
                _Logger.LogInformation("创建新游客会话 - SessionKey: {SessionKey}", session.Key);
            }

            // 4. 将用户信息注入 HttpContext.Items，供后续管道使用
            context.Items[ContextKeyName_DomainUser] = session.User!;
            // 5. 构造 ClaimsPrincipal 并注入 HttpContext.User，供 [Authorize] 等特性使用
            context.User = session.User!.ToClaimsPrincipal();

            await next(context);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "SessionUserMiddleware 执行异常");
            // 注意：这里不再直接处理响应（WriteAsync），而是抛出异常。
            // 这样做的好处是让外层的全局异常过滤器（如 WebExceptionMiddleware）统一处理错误格式，
            // 保证 API 返回的 JSON 格式一致性，避免中间件直接写入文本导致的格式混乱。
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
            MaxAge = options.MaxAge
        });

        // 同时也写入 Header，方便非浏览器客户端（如 App、小程序）获取
        context.Response.Headers.Append(options.HeaderName, sessionKey);
    }
}
