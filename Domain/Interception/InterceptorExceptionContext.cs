using System;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 拦截器异常上下文：记录 AOP 拦截过程中的异常详情
/// </summary>
public class InterceptorExceptionContext(InvocationContext invocation, Exception exception)
{
    /// <summary>
    /// V4 静态调用上下文
    /// </summary>
    public InvocationContext Invocation { get; } = invocation ?? throw new ArgumentNullException(nameof(invocation));

    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ExceptionHandled { get; set; }

    // 辅助属性，用于日志记录
    public string? Method { get; set; }
    public string? UserName { get; set; }
    public string? TargetType { get; set; }
    public bool IsAuthenticationError { get; set; }
    public bool IsAuthorizationError { get; set; }
}