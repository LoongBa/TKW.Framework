using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Interception.Filters;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域核心配置项，不包含任何 Web 环境依赖
/// </summary>
public class DomainOptions
{
    public bool IsDevelopment { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public Dictionary<string, string> ConfigDictionary { get; set; } = new();
    public EnumDomainLogLevel LoggingLevel { get; set; } = EnumDomainLogLevel.Minimal;

    public Type? SessionManagerType { get; set; }
    public Type? ExceptionLoggerFactoryType { get; set; }
}
