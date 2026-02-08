using Castle.DynamicProxy;
using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public abstract class BaseInterceptor<TUserInfo> : IAsyncInterceptor
    where TUserInfo : class, IUserInfo, new()
{
    private static readonly AsyncLocal<InterceptorContext<TUserInfo>?> _currentContext = new();

    public static InterceptorContext<TUserInfo>? CurrentContext => _currentContext.Value;

    protected DomainContext<TUserInfo>? Context { get; set; }

    protected abstract Task InitialAsync(IInvocation invocation);

    protected virtual Task PreProceedAsync(IInvocation invocation) => Task.CompletedTask;

    protected virtual Task PostProceedAsync(IInvocation invocation) => Task.CompletedTask;

    protected abstract void OnException(InterceptorExceptionContext context);

    public void InterceptSynchronous(IInvocation invocation)
    {
        Intercept(invocation);
    }

    public void InterceptSynchronousResult(IInvocation invocation)
    {
        Intercept(invocation);
    }

    public async void InterceptAsynchronous(IInvocation invocation)
    {
        try
        {
            await InitialAsync(invocation);
            Context.EnsureNotNull();

            _currentContext.Value = new InterceptorContext<TUserInfo>
            {
                Invocation = invocation,
                DomainContext = Context
            };

            await PreProceedAsync(invocation);
            invocation.Proceed();
            await PostProceedAsync(invocation);
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), ex);
            OnException(ctx);

            if (!ctx.ExceptionHandled)
                // ReSharper disable once AsyncVoidThrowException
                throw;
        }
        finally
        {
            _currentContext.Value = null;
        }
    }

    public async void InterceptAsynchronous<TResult>(IInvocation invocation)
    {
        try
        {
            await InitialAsync(invocation);
            Context.EnsureNotNull();

            _currentContext.Value = new InterceptorContext<TUserInfo>
            {
                Invocation = invocation,
                DomainContext = Context
            };

            await PreProceedAsync(invocation);
            invocation.Proceed();
            await PostProceedAsync(invocation);
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), ex);
            OnException(ctx);

            if (!ctx.ExceptionHandled)
                throw;
        }
        finally
        {
            _currentContext.Value = null;
        }
    }

    private void Intercept(IInvocation invocation)
    {
        throw new NotSupportedException("同步拦截在新版本中已弃用，请使用异步方法");
    }
}

/// <summary>
/// 拦截器上下文
/// </summary>
public class InterceptorContext<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public required IInvocation Invocation { get; init; }
    public required DomainContext<TUserInfo>? DomainContext { get; init; }
}