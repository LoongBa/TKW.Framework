using System;
using System.Linq;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 记录历史（忽略 IgnoreEntityHistoryAttribute）
/// </summary>
/// <see cref="IgnoreEntityHistoryAttribute"/>
public class EntityHistoryActionFilterAttribute<TUserInfo> : DomainActionFilterAttribute<TUserInfo>
where TUserInfo : class, IUserInfo, new()
{
    #region Overrides of DomainActionFilterAttribute

    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        return invocationWhere switch
        {
            DomainInvocationWhereType.Method => true,
            DomainInvocationWhereType.Controller =>
                !context.MethodFlags.Any(f => f is IgnoreEntityHistoryAttribute),
            DomainInvocationWhereType.Global =>
                !(context.ControllerFlags.Any(f => f is IgnoreEntityHistoryAttribute) ||
                  context.MethodFlags.Any(f => f is IgnoreEntityHistoryAttribute)),
            _ => throw new ArgumentOutOfRangeException(nameof(invocationWhere), invocationWhere, null)
        };
    }

    public override void PreProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context) { }
    public override void PostProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context) { }

    #endregion
}