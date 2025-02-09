using System;
using System.Collections.ObjectModel;

namespace TKW.Framework.Domain.Interception {
    public abstract class DomainExceptionFilterAttribute : Attribute
    {
        public abstract void OnException(DomainContext context, ReadOnlyCollection<DomainExceptionFilterAttribute> filters);
    }
}