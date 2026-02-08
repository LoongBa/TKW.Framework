using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TKW.Framework.Domain.Interception;

public class DefaultDomainGlobalExceptionFactory(ILogger logger)
{
    public async Task HandleExceptionAsync(InterceptorExceptionContext context)
    {
        HandleException(context);
        await Task.CompletedTask;
    }

    public virtual void HandleException(InterceptorExceptionContext context)
    {
        var ex = context.Exception;
        var method = context.Invocation.Method.Name;
        var userName = context.UserName ?? "Anonymous";

        // 统一日志
        logger.LogError(ex,
            "领域层未捕获异常 - 方法: {Method} - 用户: {UserName} - 位置: {Where}",
            method, userName, context.Invocation.InvocationTarget?.GetType().Name);

        // 标记已处理（防止上层重复抛出）
        context.ExceptionHandled = true;
    }
}