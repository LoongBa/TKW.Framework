using System.Security.Claims;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Web.Users;

public static class DomainUserExtensions
{
    /// <summary>
    /// 转换为新的 ClaimsPrincipal 实例，以兼容 ASP.NET 身份验证体系
    /// </summary>
    public static ClaimsPrincipal ToNewClaimsPrincipal<TUserInfo>(this DomainUser<TUserInfo> user)
        where TUserInfo : class, IUserInfo, new()
    {
        var claimsIdentity = new ClaimsIdentity(user.IsAuthenticated ? "Authenticated" : string.Empty);
        claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserInfo.UserIdString));
        claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.UserInfo.UserName));
        var newPrincipal = new ClaimsPrincipal(claimsIdentity);

        return newPrincipal;
    }
}