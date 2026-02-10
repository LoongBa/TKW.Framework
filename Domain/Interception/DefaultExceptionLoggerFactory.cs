using Microsoft.Extensions.Logging;
using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interception.Filters;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TKW.Framework.Domain.Interception;

public class DefaultExceptionLoggerFactory
{
    private ILogger? _Logger;
    private EnumDomainLogLevel _LogLevel = EnumDomainLogLevel.None;
    public async Task HandleExceptionAsync(InterceptorExceptionContext context)
    {
        LogException(context);
        await Task.CompletedTask;
    }

    protected internal DefaultExceptionLoggerFactory SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        _Logger = loggerFactory.CreateLogger("ExceptionLogger");
        return this;
    }

    public virtual void LogException(InterceptorExceptionContext context)
    {
        var ex = context.Exception;
        // 根据级别决定是否记录及日志级别
        if (_LogLevel >= EnumDomainLogLevel.Minimal)
            _Logger?.LogError(ex,
                "领域层未捕获异常 - 方法: {Method} - 用户: {UserName} - 目标类型: {TargetType}",
                context.Method, context.UserName, context.TargetType);

        if (_LogLevel >= EnumDomainLogLevel.Normal && ex is AuthenticationException or UnauthorizedAccessException)
            _Logger?.LogWarning("认证/授权异常 - {Message}", ex.Message);

        if (_LogLevel >= EnumDomainLogLevel.Verbose)
            _Logger?.LogDebug("异常详情 - StackTrace: {Stack}", ex.StackTrace);

        // Debug 模式下额外输出到控制台（便于开发调试）
#if DEBUG
        System.Diagnostics.Debug.WriteLine("=== DEBUG 异常捕获 ===");
        System.Diagnostics.Debug.WriteLine($"方法: {context.Method}");
        System.Diagnostics.Debug.WriteLine($"用户: {context.UserName}");
        System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
        System.Diagnostics.Debug.WriteLine($"消息: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
        System.Diagnostics.Debug.WriteLine("====================");
#endif
    }

    internal DefaultExceptionLoggerFactory? SetLogLevel(EnumDomainLogLevel logLevel)
    {
        _LogLevel = logLevel;
        return this;
    }
}