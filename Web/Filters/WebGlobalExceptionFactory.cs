using System;
using System.Security.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interception;

namespace TKW.Framework.Web.Filters;

/// <summary>
/// Web 环境下的默认领域异常工厂，继承自 DefaultDomainGlobalExceptionFactory
/// </summary>
/// <param name="logger"></param>
/// <param name="httpContextAccessor"></param>
public class WebGlobalExceptionFactory(ILogger logger, IHttpContextAccessor httpContextAccessor)
    : DefaultDomainGlobalExceptionFactory(logger)
{
    public override void HandleException(InterceptorExceptionContext context)
    {
        var ex = context.Exception;

        // 设置 HTTP 响应（如果在 Web 环境）
        var response = httpContextAccessor.HttpContext?.Response;
        if (response != null)
        {
            response.StatusCode = ex switch
            {
                AuthenticationException => StatusCodes.Status401Unauthorized,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ValidationException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            // 可选：写入 JSON 错误体（HotChocolate / FastEndpoint 会处理）
            response.ContentType = "application/json";
            var errorResponse = new
            {
                error = "服务器内部错误",
                message = ex.Message,
                code = response.StatusCode,
                // traceId = Activity.Current?.Id （可加追踪ID）
            };
            response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
        }

        // 调用基类日志记录，并标记为已处理
        base.HandleException(context);
    }
}