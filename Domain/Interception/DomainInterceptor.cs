using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public class DomainInterceptor<TUserInfo> : BaseInterceptor<TUserInfo>, IDisposable
    where TUserInfo : class, IUserInfo, new()
{
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly DefaultExceptionLoggerFactory? _GlobalExceptionLoggerFactory;

    public DomainInterceptor(DomainHost<TUserInfo> domainHost)
    {
        ArgumentNullException.ThrowIfNull(domainHost);
        _DomainHost = domainHost;
        _GlobalExceptionLoggerFactory = domainHost.ExceptionLoggerFactory;
    }

    #region 生命周期：物理隔离作用域开启与关闭

    // 【修复3】：合并重复的初始化逻辑，剥离多余的 async
    private void InitializeScope(IInvocation invocation)
    {
        if (_DomainHost.Container == null)
            throw new InvalidOperationException("领域主机尚未完成初始化，无法开启拦截作用域。");

        var perCallScope = _DomainHost.Container.BeginLifetimeScope();
        Context = _DomainHost.NewDomainContext(invocation, perCallScope);
        Context.EnsureNotNull();
    }

    protected override void InitialSync(IInvocation invocation) => InitializeScope(invocation);

    protected override Task InitialAsync(IInvocation invocation)
    {
        InitializeScope(invocation);
        return Task.CompletedTask;
    }

    protected override void CleanUpSync()
    {
        if (Context?.LifetimeScope != null)
        {
            Context.LifetimeScope.Dispose();
        }
    }

    protected override async Task CleanUpAsync()
    {
        if (Context?.LifetimeScope != null)
        {
            await Context.LifetimeScope.DisposeAsync();
        }
    }

    #endregion

    #region 异步过滤器流 (保持你原有的遍历逻辑)

    protected override async Task PreProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        foreach (var filter in _DomainHost.GlobalFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Global, Context);

        var controllerFilters = Context.ControllerFilters.Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId));
        foreach (var filter in controllerFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Controller, Context);

        foreach (var filter in Context.MethodFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Method, Context);
    }

    protected override async Task PostProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        foreach (var filter in Context.MethodFilters.AsEnumerable().Reverse())
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Method, Context);

        var controllerFilters = Context.ControllerFilters.Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId)).AsEnumerable().Reverse();
        foreach (var filter in controllerFilters)
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Controller, Context);

        foreach (var filter in _DomainHost.GlobalFilters.AsEnumerable().Reverse())
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Global, Context);
    }

    #endregion

    #region 同步过滤器流 (仅作兼容占位)

    protected override void PreProceedSync(IInvocation invocation) { }

    protected override void PostProceedSync(IInvocation invocation) { }

    #endregion

    protected override void LogException(InterceptorExceptionContext context)
    {
        // 异常日志处理逻辑保持原样
        Context?.Logger?.LogError(context.Exception,
            "领域方法执行异常 | 方法: {MethodName} | 用户: {UserName}",
            context.Invocation.Method.Name,
            Context.DomainUser.UserInfo.UserName);

        var ex = context.Exception;
        context.ErrorMessage = ex.Message;
        context.IsAuthenticationError = ex is AuthenticationException;
        context.IsAuthorizationError = ex is UnauthorizedAccessException;
        context.Method = context.Invocation.Method.Name;
        context.UserName = Context?.DomainUser?.UserInfo?.UserName ?? "Unknown";
        context.TargetType = context.Invocation.InvocationTarget.GetType().Name;

        if (context.IsAuthenticationError) context.ErrorCode = "AUTH_001";
        else if (context.IsAuthorizationError) context.ErrorCode = "AUTH_002";

        _GlobalExceptionLoggerFactory?.LogException(context);
        context.ExceptionHandled = false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}