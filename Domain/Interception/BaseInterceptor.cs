using Castle.DynamicProxy;
using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 异步拦截器基类（所有领域拦截器应继承此类）
/// 使用 Castle.Core.AsyncInterceptor 实现真正的异步拦截支持
/// </summary>
/// <remarks>
/// 同时实现 IInterceptor 和 IAsyncInterceptor，以兼容 Autofac 的 EnableInterfaceInterceptors()
/// </remarks>
public abstract class BaseInterceptor<TUserInfo> : AsyncInterceptorBase, IInterceptor
    where TUserInfo : class, IUserInfo, new()
{
    private static readonly AsyncLocal<InterceptorContext<TUserInfo>?> _currentContext = new();

    /// <summary>
    /// 当前拦截上下文（线程安全，通过 AsyncLocal 传递）
    /// </summary>
    public static InterceptorContext<TUserInfo>? CurrentContext => _currentContext.Value;

    /// <summary>
    /// 当前领域上下文（由子类在 InitialAsync 中设置）
    /// </summary>
    protected DomainContext<TUserInfo>? Context { get; set; }

    /// <summary>
    /// 初始化拦截上下文（子类必须实现）
    /// </summary>
    protected abstract Task InitialAsync(IInvocation invocation);

    /// <summary>
    /// 前置处理（子类可重写）
    /// </summary>
    protected virtual Task PreProceedAsync(IInvocation invocation) => Task.CompletedTask;

    /// <summary>
    /// 后置处理（子类可重写）
    /// </summary>
    protected virtual Task PostProceedAsync(IInvocation invocation) => Task.CompletedTask;

    /// <summary>
    /// 异常处理（子类必须实现）
    /// </summary>
    protected abstract void LogException(InterceptorExceptionContext context);

    /// <summary>
    /// 无返回值异步方法拦截实现
    /// </summary>
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

    /// <summary>
    /// 有返回值异步方法拦截实现
    /// </summary>
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

    /// <summary>
    /// 同步拦截强制转发到异步实现（避免同步/异步混用导致死锁或异常扭曲）
    /// TODO: 等待官方解决
    /// </summary>
    /// <remarks>
    /// 1. 使用 GetAwaiter().GetResult() 会阻塞调用线程，需谨慎使用（仅用于兼容旧同步调用）
    /// 2. 如果项目完全异步化，可改为直接抛出 NotSupportedException
    /// </remarks>
    public void Intercept(IInvocation invocation)
    {
        try
        {
            // 捕获 ProceedInfo，确保在异步上下文中正确执行
            var proceedInfo = invocation.CaptureProceedInfo();

            // 转发到异步版本，并阻塞等待完成
            InterceptAsync(
                invocation,
                proceedInfo,
                (inv, info) =>
                {
                    info.Invoke();
                    return Task.CompletedTask;
                }
            ).GetAwaiter().GetResult();
        }
        catch
        {
            // 同步路径下异常也要正确传播（避免被 async void 吃掉）
            throw;
        }
    }

    /// <summary>
    /// 抽取公共拦截逻辑（避免代码重复）
    /// </summary>
    private async Task ExecuteCommonLogicAsync(
        IInvocation invocation,
        Func<Task> innerProceed)
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

            await innerProceed();

            await PostProceedAsync(invocation);
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(
                new DomainMethodInvocation(invocation),
                ex);

            LogException(ctx);
            // 不要在这里吃掉异常，交由调用方处理（保持原有异常传播行为）
            //if (!ctx.ExceptionHandled) 
            throw;
        }
        finally
        {
            _currentContext.Value = null;
        }
    }
}