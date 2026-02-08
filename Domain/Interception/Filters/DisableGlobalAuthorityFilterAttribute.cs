using System;

namespace TKW.Framework.Domain.Interception.Filters;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public class DisableGlobalAuthorityFilterAttribute : DomainFlagAttribute { }