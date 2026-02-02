using System;
using System.Collections.ObjectModel;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public abstract class DomainExceptionFilterAttribute : Attribute
{
    public abstract void OnException<TUserInfo>(DomainContext<TUserInfo> context, ReadOnlyCollection<DomainExceptionFilterAttribute> filters)
        where TUserInfo : class, IUserInfo, new();
}