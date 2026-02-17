using Castle.DynamicProxy;
using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 异步拦截器基类
/// 支持真正的异步拦截，并提供基于 AsyncLocal 的上下文隔离。
/// </summary>
public abstract class BaseInterceptor<TUserInfo> : AsyncInterceptorBase, IInterceptor
    where TUserInfo : class, IUserInfo, new()
{
    private static readonly AsyncLocal<InterceptorContext<TUserInfo>?> _currentContext = new();

    /// <summary>
    /// 当前拦截上下文（逻辑流隔离，防止并发冲突）
    /// </summary>
    public static InterceptorContext<TUserInfo>? CurrentContext => _currentContext.Value;

    /// <summary>
    /// 当前领域上下文
    /// </summary>
    protected DomainContext<TUserInfo>? Context { get; set; }

    /// <summary>
    /// 初始化拦截上下文（子类实现作用域开启逻辑）
    /// </summary>
    protected abstract Task InitialAsync(IInvocation invocation);

    /// <summary>
    /// 拦截结束后的清理钩子（用于释放资源）
    /// </summary>
    protected virtual Task CleanUpAsync() => Task.CompletedTask;

    protected virtual Task PreProceedAsync(IInvocation invocation) => Task.CompletedTask;
    protected virtual Task PostProceedAsync(IInvocation invocation) => Task.CompletedTask;
    protected abstract void LogException(InterceptorExceptionContext context);

    #region 异步拦截实现

    protected override async Task InterceptAsync(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        await ExecuteCommonLogicAsync(invocation, async () =>
        {
            await proceed(invocation, proceedInfo);
        });
    }

    protected override async Task<TResult> InterceptAsync<TResult>(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        TResult? result = default;
        await ExecuteCommonLogicAsync(invocation, async () =>
        {
            result = await proceed(invocation, proceedInfo);
        });
        return result!;
    }

    #endregion

    /// <summary>
    /// 同步拦截兼容性转发
    /// </summary>
    public void Intercept(IInvocation invocation)
    {
        try
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            InterceptAsync(invocation, proceedInfo, (inv, info) =>
            {
                info.Invoke();
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
        catch { throw; }
    }

    /// <summary>
    /// 核心拦截逻辑模板
    /// </summary>
    private async Task ExecuteCommonLogicAsync(
        IInvocation invocation,
        Func<Task> innerProceed)
    {
        try
        {
            // 1. 初始化（此时子类会开启独立的物理作用域）
            await InitialAsync(invocation);
            Context.EnsureNotNull();

            // 2. 设置 AsyncLocal 上下文，供过滤器访问
            _currentContext.Value = new InterceptorContext<TUserInfo>
            {
                Invocation = invocation,
                DomainContext = Context
            };

            await PreProceedAsync(invocation);
            await innerProceed();
            await PostProceedAsync(invocation);
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), ex);
            LogException(ctx);
            throw;
        }
        finally
        {
            // 【关键重构】：无论执行成功还是失败，必须清理上下文并触发资源释放
            await CleanUpAsync();
            _currentContext.Value = null;
        }
    }
}