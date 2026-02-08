using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 领域拦截器（核心 AOP 组件）：负责注入 DomainContext、执行前后置过滤器、异常处理
/// </summary>
/// <remarks>
/// 1. 支持方法级、控制器级、全局级过滤器
/// 2. 通过 DomainContext 自动注入当前 DomainUser<TUserInfo>
/// 3. 支持异步过滤器（Pre/PostProceedAsync）
/// 4. 全局异常统一处理（可扩展）
/// </remarks>
public class DomainInterceptor<TUserInfo> : BaseInterceptor<TUserInfo>, IDisposable
    where TUserInfo : class, IUserInfo, new()
{
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly IDomainGlobalExceptionFactory _GlobalExceptionFactory;
    private readonly ILifetimeScope _LifetimeScope;

    // 全局过滤器列表（静态注册，建议在 DomainHost 初始化时添加）
    private static readonly List<DomainFilterAttribute<TUserInfo>> GlobalFilters =
    [
        new LoggingFilterAttribute<TUserInfo>(), // 示例全局日志
        new AuthorityFilterAttribute<TUserInfo>() // 示例全局授权
        // new TransactionFilterAttribute<TUserInfo>()  // 示例事务
    ];

    public DomainInterceptor(DomainHost<TUserInfo> domainHost, IDomainGlobalExceptionFactory globalExceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(domainHost);
        ArgumentNullException.ThrowIfNull(domainHost.Container);

        _DomainHost = domainHost;
        _GlobalExceptionFactory = globalExceptionFactory;
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

        // 全局过滤器
        foreach (var filter in GlobalFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Global, Context);
        }

        // 控制器级（排除重复）
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

        // 方法级后置
        foreach (var filter in Context.MethodFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Method, Context);
        }

        // 控制器级后置
        var controllerFilters = Context.ControllerFilters
            .Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId))
            .AsEnumerable()
            .Reverse();

        foreach (var filter in controllerFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Controller, Context);
        }

        // 全局后置
        foreach (var filter in GlobalFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Global, Context);
        }
    }

    protected override void OnException(InterceptorExceptionContext context)
    {
        Context?.Logger.LogError(context.Exception,
            "Domain 方法异常: {MethodName} - 用户: {UserName}",
            context.Invocation.Method.Name,
            Context.DomainUser.UserInfo.UserName);

        _GlobalExceptionFactory.HandleException(context);

        context.ExceptionHandled = true;
    }

    public void Dispose()
    {
        _LifetimeScope.Dispose();
        GC.SuppressFinalize(this);
    }
}