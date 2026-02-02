// TKW.Domain.Web / Middleware / SessionUserMiddleware.cs

#nullable enable
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Web.Users;

namespace TKW.Framework.Web.Middlewares;

/// <summary>
/// 从 Cookie / Header 读取 SessionKey，加载 DomainUser 并注入 HttpContext
/// 支持 Blazor Server 和 WebAPI
/// </summary>
public class SessionUserMiddleware<TUserInfo>(RequestDelegate next, ISessionManager<TUserInfo> sessionManager)
    where TUserInfo : class, IUserInfo, new()

{
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. 尝试从 Cookie / Header / Query 获取 SessionKey
        var sessionKey = context.Request.Cookies["SessionKey"]
                         ?? context.Request.Headers["X-Session-Key"].FirstOrDefault()
                         ?? context.Request.Query["sessionKey"].ToString();

        DomainUser<TUserInfo>? currentUser = null;

        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            var session = await sessionManager.GetSessionAsync(sessionKey);
            currentUser = session?.User;
        }

        if (currentUser != null)
        {
            // 2. 构造极简 ClaimsPrincipal（只带 Authenticated 和 Roles）
            var claimsPrincipal = currentUser.ToNewClaimsPrincipal();
            //TODO: 只添加 Role Claim：注意，采用 Claim 还是 PermissionSet，最后需要统一

            context.User = claimsPrincipal;
        }

        // 3. 注入到 Items，供 Controller / Blazor 使用
        context.Items["CurrentDomainUser"] = currentUser;

        await next(context);
    }
}