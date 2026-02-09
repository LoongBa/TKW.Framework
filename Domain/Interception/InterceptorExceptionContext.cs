using System;

namespace TKW.Framework.Domain.Interception;

public record InterceptorExceptionContext(
    DomainMethodInvocation Invocation, Exception Exception)
{
    public bool ExceptionHandled { get; set; } = false;

    // 供表现层和测试代码使用
    public string? ErrorMessage { get; set; }
    public bool IsAuthenticationError { get; set; }
    public bool IsAuthorizationError { get; set; }
    public string? ErrorCode { get; set; }
    public string? TargetType { get; set; }
    public string? Method { get; set; }
    public string? UserName { get; set; }
}