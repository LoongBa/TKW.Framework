using System;

namespace TKW.Framework.Domain.Interception;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public abstract class DomainFlagAttribute : Attribute
{
}