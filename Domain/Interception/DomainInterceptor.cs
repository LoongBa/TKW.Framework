using Autofac;
using Castle.Core.Logging;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 领域拦截器（核心 AOP 组件）：负责注入 DomainContext、执行前后置过滤器、异常处理
/// </summary>
/// <remarks>
/// 1. 支持方法级、控制器级、全局级过滤器（全局 Filter 由 DomainHost 统一管理）
/// 2. 通过 DomainContext 自动注入当前 DomainUser{TUserInfo}
/// 3. 支持异步过滤器（Pre/PostProceedAsync）
/// 4. 全局异常统一处理（可被应用层替换）
/// 
/// 注意：全局过滤器默认为空，需要通过 DomainHost.AddGlobalFilter 或 EnableDomainLogging 等方式显式启用。
/// </remarks>
public class DomainInterceptor<TUserInfo> : BaseInterceptor<TUserInfo>, IDisposable
    where TUserInfo : class, IUserInfo, new()
{
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly DefaultExceptionLoggerFactory? _GlobalExceptionLoggerFactory;
    private readonly ILifetimeScope _LifetimeScope;

    public DomainInterceptor(DomainHost<TUserInfo> domainHost)
    {
        ArgumentNullException.ThrowIfNull(domainHost);
        ArgumentNullException.ThrowIfNull(domainHost.Container);

        _DomainHost = domainHost;
        _GlobalExceptionLoggerFactory = domainHost.ExceptionLoggerFactory;
        _LifetimeScope = _DomainHost.Container.BeginLifetimeScope();
    }

    protected override async Task InitialAsync(IInvocation invocation)
    {
        Context = _DomainHost.NewDomainContext(invocation, _LifetimeScope);
        Context.EnsureNotNull();

        await Task.CompletedTask;
    }

    protected override async Task PreProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        // 全局过滤器（从 DomainHost 获取）
        foreach (var filter in _DomainHost.GlobalFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Global, Context);
        }

        // 控制器级（排除与方法级重复的 Filter）
        var controllerFilters = Context.ControllerFilters
            .Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId));

        foreach (var filter in controllerFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Controller, Context);
        }

        // 方法级
        foreach (var filter in Context.MethodFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Method, Context);
        }
    }

    protected override async Task PostProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        // 方法级后置（逆序执行）
        foreach (var filter in Context.MethodFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Method, Context);
        }

        // 控制器级后置（逆序执行）
        var controllerFilters = Context.ControllerFilters
            .Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId))
            .AsEnumerable()
            .Reverse();

        foreach (var filter in controllerFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Controller, Context);
        }

        // 全局后置（逆序执行）
        foreach (var filter in _DomainHost.GlobalFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Global, Context);
        }
    }

    protected override void LogException(InterceptorExceptionContext context)
    {
        Context?.Logger?.LogError(context.Exception,
            "Domain 方法异常: {MethodName} - 用户: {UserName}",
            context.Invocation.Method.Name,
            Context.DomainUser.UserInfo.UserName);

        if (context == null || context.Exception == null)
            return;

        var ex = context.Exception;

        // 安全获取上下文信息
        var methodName = context.Invocation?.Method?.Name ?? "UnknownMethod";
        var userName = context.UserName
                       ?? context.UserName
                       ?? "Anonymous";
        var targetType = context.Invocation?.InvocationTarget?.GetType()?.Name ?? "UnknownTarget";

        // 把错误信息放入上下文，让上层（表现层）可以获取
        context.ErrorMessage = ex.Message;
        context.IsAuthenticationError = ex is AuthenticationException;
        context.IsAuthorizationError = ex is UnauthorizedAccessException;
        context.Method = methodName;
        context.UserName = userName;
        context.TargetType = targetType;

        if (context.IsAuthenticationError)
            context.ErrorCode = "AUTH_001";   // 未认证
        else if (context.IsAuthorizationError)
            context.ErrorCode = "AUTH_002";   // 权限不足

        // 交给全局异常日志工厂（如果有）进行统一处理（如记录日志、发送告警等）
        _GlobalExceptionLoggerFactory?.LogException(context);
        // 避免不小心被修改为 true 导致异常被吞掉
        context.ExceptionHandled = false; 
    }

    public void Dispose()
    {
        _LifetimeScope.Dispose();
        GC.SuppressFinalize(this);
    }
}