using System.Security.Claims;
using TKW.Framework.Domain;

namespace TKW.Framework.Web.Users;

public static class DomainUserExtensions
{
    extension(DomainUser user)
    {
        /// <summary>
        /// 转换为新的 ClaimsPrincipal 实例，以兼容 ASP.NET 身份验证体系
        /// </summary>
        public ClaimsPrincipal ToNewClaimsPrincipal()
        {
            var claimsIdentity = new ClaimsIdentity(user.IsAuthenticated ? "Authenticated" : string.Empty);
            claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserInfo.UserIdString));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.UserInfo.UserName));
            var newPrincipal = new ClaimsPrincipal(claimsIdentity);

            return newPrincipal;
        }
    }
}