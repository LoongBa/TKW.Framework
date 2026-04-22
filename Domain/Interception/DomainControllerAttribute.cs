using System;

namespace TKW.Framework.Domain.Interception;

/// <summary>ÁìÓò¿ØÖÆÆ÷ÊôĞÔ</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DomainControllerAttribute : Attribute
{
}