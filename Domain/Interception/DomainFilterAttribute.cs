using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public abstract class DomainFilterAttribute<TUserInfo> : Attribute
    where TUserInfo : class, IUserInfo, new()
{
    public virtual bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context) => true;

    public abstract Task PreProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context);
    public abstract Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context);
}