using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public abstract class DomainExceptionFilterAttribute : Attribute
{
    public abstract Task OnExceptionAsync<TUserInfo>(DomainContext<TUserInfo> context, ReadOnlyCollection<DomainExceptionFilterAttribute> filters)
        where TUserInfo : class, IUserInfo, new();
}