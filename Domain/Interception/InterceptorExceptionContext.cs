using System;

namespace TKW.Framework.Domain.Interception;

public record InterceptorExceptionContext(DomainMethodInvocation Invocation, Exception Exception)
{
    public bool Continue { get; set; } = false;
    public bool ExceptionHandled { get; set; } = false;
    public string? ExceptionType => Exception.GetType().Name;  // 辅助日志
    public string UserName { get; set; } = string.Empty; // 辅助日志
}