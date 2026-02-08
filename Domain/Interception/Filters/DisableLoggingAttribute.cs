using System;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 禁用日志记录的标记
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface)]
public class DisableLoggingAttribute : DomainFlagAttribute { }