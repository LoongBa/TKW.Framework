using Microsoft.AspNetCore.Http;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

public abstract class WebDomainUserAccessor<TUserInfo> : IDomainUserAccessor<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    public DomainUser<TUserInfo> DomainUser { get; }

    protected WebDomainUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.GetDomainUser<TUserInfo>();
        ArgumentNullException.ThrowIfNull(user);
        DomainUser = user;
    }
}
public static class WebDomainUserAccessorExtension{
    extension(IHttpContextAccessor context)
    {
        public DomainUser<TUserInfo> GetDomainUser<TUserInfo>()
            where TUserInfo : class, IUserInfo, new()
        {
            var user = context.HttpContext?
                    .Items[SessionUserMiddleware<TUserInfo>.ContextKeyName_DomainUser]
                as DomainUser<TUserInfo>;
            ArgumentNullException.ThrowIfNull(user);
            return user;
        }
    }
}