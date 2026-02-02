using System;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public abstract class DomainActionFilterAttribute<TUserInfo> : Attribute
where TUserInfo: class, IUserInfo, new()
{
    public abstract bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context);
    public abstract void PreProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context);
    public abstract void PostProceed(DomainInvocationWhereType method, DomainContext<TUserInfo> context);
}