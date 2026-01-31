using System;

namespace TKW.Framework.Domain.Interception;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public abstract class DomainActionFilterAttribute : Attribute
{
    public abstract bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext context);
    public abstract void PreProceed(DomainInvocationWhereType method, DomainContext context);
    public abstract void PostProceed(DomainInvocationWhereType method, DomainContext context);
}