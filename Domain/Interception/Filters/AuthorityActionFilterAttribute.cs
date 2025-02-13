using System;
using System.Linq;
using System.Security.Authentication;

namespace TKW.Framework.Domain.Interception.Filters
{
    /// <summary>
    /// 权限验证（忽略 AllowAnonymousAttribute）
    /// </summary>
    /// <see cref="AllowAnonymousAttribute"/>
    public class AuthorityActionFilterAttribute : DomainActionFilterAttribute
    {
        #region Overrides of DomainActionFilterAttribute

        public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext context)
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

        public override void PreProceed(DomainInvocationWhereType method, DomainContext context)
        {
            var user = context.DomainUser;
            if (!user.Identity.IsAuthenticated) 
                throw new AuthenticationException($"用户 '{user.Identity.Name}' 未认证。");
        }

        public override void PostProceed(DomainInvocationWhereType method, DomainContext context)
        {
        }

        #endregion
    }
}