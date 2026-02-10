using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Exceptions;

namespace TKW.Framework.Domain.Web.Middlewares;

/// <summary>
/// 统一异常处理中间件（推荐放在 UseRouting / UseEndpoints 之后）
/// 捕获所有未处理的异常，返回标准化的 JSON 错误响应
/// 支持开发模式显示详细堆栈、生产模式隐藏敏感信息
/// </summary>
public class DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<DomainExceptionMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // 记录完整异常（包括堆栈）
        _logger.LogError(exception, "未处理的异常 - 请求路径: {Path} - 方法: {Method}",
            context.Request.Path, context.Request.Method);

        // 统一响应格式
        var response = new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Message = "服务器内部错误",
            ErrorCode = "INTERNAL_ERROR",
            Details = null
        };

        // 根据异常类型设置状态码和消息
        if (exception is AuthenticationException authEx)
        {
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.Message = "未认证，请登录";
            response.ErrorCode = "AUTH_UNAUTHENTICATED";
            response.Details = authEx.Message;  // 可选：开发模式显示更多
        }
        else if (exception is UnauthorizedAccessException unAuthEx)
        {
            response.StatusCode = (int)HttpStatusCode.Forbidden;
            response.Message = "权限不足";
            response.ErrorCode = "AUTH_FORBIDDEN";
            response.Details = unAuthEx.Message;
        }
        else if (exception is DomainException domainEx)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.Message = domainEx.Message;
            response.ErrorCode = domainEx.ErrorCode ?? "DOMAIN_ERROR";
            response.Details = domainEx.Data;  // 如果有额外数据
        }

        // 开发模式下附加堆栈信息（生产环境隐藏）
#if DEBUG
        response.Details = new
        {
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.Message
        };
#endif

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    // 统一错误响应模型
    private class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public object? Details { get; set; }
    }
}