using System;
using System.Linq;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <inheritdoc />
public class ContentCacheFilterAttribute<TUserInfo> : DomainFilterAttribute<TUserInfo>
where TUserInfo: class, IUserInfo, new()
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

    public override Task PreProceedAsync(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
        //判断是否有缓存
        return Task.CompletedTask;
    }

    public override Task PostProceedAsync(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
        //更新缓存
        return Task.CompletedTask;
    }

    #endregion
}