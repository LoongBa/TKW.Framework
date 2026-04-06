using Castle.DynamicProxy;
using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 异步拦截器基类
/// 【终极安全版】：解决死锁，消灭并发状态竞争，完全兼容 Autofac 与 Castle 代理。
/// </summary>
public abstract class BaseInterceptor<TUserInfo> : IAsyncInterceptor, IInterceptor
    where TUserInfo : class, IUserInfo, new()
{
    private static readonly AsyncLocal<InterceptorContext<TUserInfo>?> _currentContext = new();

    // 【修复1】：将 Context 放入 AsyncLocal，防止单例拦截器并发时的状态覆盖
    private static readonly AsyncLocal<DomainContext<TUserInfo>?> _domainContext = new();

    public static InterceptorContext<TUserInfo>? CurrentContext => _currentContext.Value;

    protected DomainContext<TUserInfo>? Context
    {
        get => _domainContext.Value;
        set => _domainContext.Value = value;
    }

    protected BaseInterceptor()
    {
        // 构造函数不再持有 _adapter 实例字段
    }

    #region 生命周期抽象钩子

    protected abstract void InitialSync(IInvocation invocation);
    protected abstract Task InitialAsync(IInvocation invocation);

    protected virtual void CleanUpSync() { }
    protected virtual Task CleanUpAsync() => Task.CompletedTask;

    protected virtual void PreProceedSync(IInvocation invocation) { }
    protected virtual Task PreProceedAsync(IInvocation invocation) => Task.CompletedTask;

    protected virtual void PostProceedSync(IInvocation invocation) { }
    protected virtual Task PostProceedAsync(IInvocation invocation) => Task.CompletedTask;

    protected abstract void LogException(InterceptorExceptionContext context);

    #endregion

    #region 1. 兼容 Autofac 的原生入口

    /// <summary>
    /// 当 Autofac 触发拦截时，进入这里。
    /// </summary>
    public void Intercept(IInvocation invocation)
    {
        // 【修复2】：按需创建轻量级适配器，消除共享状态竞争
        new AsyncDeterminationInterceptor(this).Intercept(invocation);
    }

    #endregion

    #region 2. 真正的同步流拦截逻辑

    public void InterceptSynchronous(IInvocation invocation)
    {
        try
        {
            InitialSync(invocation);
            Context.EnsureNotNull();
            _currentContext.Value = new InterceptorContext<TUserInfo> { Invocation = invocation, DomainContext = Context };

            PreProceedSync(invocation);

            invocation.Proceed(); // 执行原同步方法

            PostProceedSync(invocation);
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), ex);
            LogException(ctx);
            throw;
        }
        finally
        {
            CleanUpSync(); // 必须是同步释放！

            // 【关键】：同时清理两个 AsyncLocal 上下文
            _currentContext.Value = null;
            Context = null;
        }
    }

    #endregion

    #region 3. 真正的异步流拦截逻辑

    public void InterceptAsynchronous(IInvocation invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        invocation.ReturnValue = ExecuteAsync(invocation, proceedInfo);
    }

    public void InterceptAsynchronous<TResult>(IInvocation invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        invocation.ReturnValue = ExecuteAsyncWithResult<TResult>(invocation, proceedInfo);
    }

    private async Task ExecuteAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo)
    {
        try
        {
            await InitialAsync(invocation);
            Context.EnsureNotNull();
            _currentContext.Value = new InterceptorContext<TUserInfo> { Invocation = invocation, DomainContext = Context };

            await PreProceedAsync(invocation);

            proceedInfo.Invoke(); // 触发原异步方法
            var task = (Task)invocation.ReturnValue;
            await task.ConfigureAwait(false);

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
            await CleanUpAsync(); // 安全的异步释放
            _currentContext.Value = null;
            Context = null;
        }
    }

    private async Task<TResult> ExecuteAsyncWithResult<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo)
    {
        try
        {
            await InitialAsync(invocation);
            Context.EnsureNotNull();
            _currentContext.Value = new InterceptorContext<TUserInfo> { Invocation = invocation, DomainContext = Context };

            await PreProceedAsync(invocation);

            proceedInfo.Invoke(); // 触发原异步方法
            var task = (Task<TResult>)invocation.ReturnValue;
            var result = await task.ConfigureAwait(false);

            await PostProceedAsync(invocation);
            return result;
        }
        catch (Exception ex)
        {
            var ctx = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), ex);
            LogException(ctx);
            throw;
        }
        finally
        {
            await CleanUpAsync(); // 安全的异步释放
            _currentContext.Value = null;
            Context = null;
        }
    }

    #endregion
}