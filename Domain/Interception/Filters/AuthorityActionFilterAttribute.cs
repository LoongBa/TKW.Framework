using System;
using System.Linq;
using System.Security.Authentication;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 权限验证（忽略 AllowAnonymousAttribute）
/// </summary>
/// <see cref="AllowAnonymousAttribute"/>
public class AuthorityActionFilterAttribute<TUserInfo> : DomainActionFilterAttribute<TUserInfo>
where TUserInfo: class, IUserInfo, new()
{
    #region Overrides of DomainActionFilterAttribute

    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        return invocationWhere switch
        {
            DomainInvocationWhereType.Method => true,
            DomainInvocationWhereType.Controller => !context.MethodFlags.Any(f => f is AllowAnonymousAttribute),
            DomainInvocationWhereType.Global => !(context.ControllerFlags.Any(f => f is AllowAnonymousAttribute) ||
                                                  context.MethodFlags.Any(f => f is AllowAnonymousAttribute)),
            _ => throw new ArgumentOutOfRangeException(nameof(invocationWhere), invocationWhere, null)
        };
    }

    public override void PreProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
        var user = context.DomainUser;
        if (user.IsAuthenticated == false) 
            throw new AuthenticationException($"用户 '{user.UserInfo.UserName}' 未认证。");
    }

    public override void PostProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
    }

    #endregion
}