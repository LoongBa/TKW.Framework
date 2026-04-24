using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public class StaticDomainInterceptor<TUserInfo>(DomainHost<TUserInfo> domainHost, IServiceScopeFactory scopeFactory)
    where TUserInfo : class, IUserInfo, new()
{
    private static readonly AsyncLocal<DomainContext<TUserInfo>?> _currentContext = new();
    public static DomainContext<TUserInfo>? CurrentContext => _currentContext.Value;

    public async Task InterceptAsync(InvocationContext context, Func<Task> proceed)
    {
        // 使用标准 .NET Scope 替代 Autofac LifetimeScope
        using var scope = scopeFactory.CreateScope();

        try
        {
            var domainContext = domainHost.NewDomainContext(context, scope.ServiceProvider);
            _currentContext.Value = domainContext;

            await PreProceedAsync(domainContext);
            await proceed();
            await PostProceedAsync(domainContext);
        }
        catch (Exception ex)
        {
            LogException(context, _currentContext.Value, ex);
            throw;
        }
        finally
        {
            _currentContext.Value = null; // 确保清理上下文
        }
    }

    private async Task PreProceedAsync(DomainContext<TUserInfo> context)
    {
        // Global
        foreach (var filter in domainHost.GlobalFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Global, context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Global, context);

        // Controller (去重：方法级已有的特性，控制器级不再执行)
        var controllerFilters = context.ControllerFilters.Where(cf => context.MethodFilters.All(mf => mf.TypeId != cf.TypeId));
        foreach (var filter in controllerFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Controller, context);

        // Method
        foreach (var filter in context.MethodFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Method, context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Method, context);
    }

    private async Task PostProceedAsync(DomainContext<TUserInfo> context)
    {
        // Method (逆序)
        foreach (var filter in context.MethodFilters.AsEnumerable().Reverse())
            if (filter.CanWeGo(DomainInvocationWhereType.Method, context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Method, context);

        // Controller (逆序)
        var controllerFilters = context.ControllerFilters.Where(cf => context.MethodFilters.All(mf => mf.TypeId != cf.TypeId)).AsEnumerable().Reverse();
        foreach (var filter in controllerFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Controller, context);

        // Global (逆序)
        foreach (var filter in domainHost.GlobalFilters.AsEnumerable().Reverse())
            if (filter.CanWeGo(DomainInvocationWhereType.Global, context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Global, context);
    }

    private void LogException(InvocationContext invContext, DomainContext<TUserInfo>? domainContext, Exception ex)
    {
        domainContext?.Logger?.LogError(ex,
            "领域方法执行异常 | 方法: {MethodName} | 用户: {UserName}",
            invContext.MethodName,
            domainContext.DomainUser.UserInfo.UserName);

        if (domainHost.ExceptionLoggerFactory == null) return;

        // 这里需要你适配一下原有的 InterceptorExceptionContext，使其接受新的 InvocationContext
        var ctx = new InterceptorExceptionContext(invContext, ex)
        {
            ErrorMessage = ex.Message,
            IsAuthenticationError = ex is AuthenticationException,
            IsAuthorizationError = ex is UnauthorizedAccessException,
            Method = invContext.MethodName,
            UserName = domainContext?.DomainUser.UserInfo.UserName ?? "Unknown",
            TargetType = invContext.Target.GetType().Name
        };

        if (ctx.IsAuthenticationError) ctx.ErrorCode = "AUTH_001";
        else if (ctx.IsAuthorizationError) ctx.ErrorCode = "AUTH_002";

        domainHost.ExceptionLoggerFactory.LogException(ctx);
    }
}