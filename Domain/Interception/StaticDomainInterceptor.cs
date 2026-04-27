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
    // 改为 internal，允许 DomainUser 访问，但不向业务层暴露可修改权
    internal static readonly AsyncLocal<DomainContext<TUserInfo>?> _currentContext = new();

    public static DomainContext<TUserInfo>? CurrentContext => _currentContext.Value;

    public async Task InterceptAsync(InvocationContext context, Func<Task> proceed)
    {
        // 使用标准 .NET Scope 替代 Autofac LifetimeScope
        using var scope = scopeFactory.CreateScope();

        // 【核心修改】：保存外层的上下文（应对 AOP 嵌套调用）
        var previousContext = _currentContext.Value;

        try
        {
            var domainContext = domainHost.NewDomainContext(context, scope.ServiceProvider);

            // 压入当前上下文
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
            // 【核心修改】：恢复外层上下文，防止嵌套拦截退出时把外层的环境清空
            _currentContext.Value = previousContext;
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