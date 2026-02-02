using Castle.DynamicProxy;
using System;
using System.Threading;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public abstract class BaseInterceptor<TUserInfo> : IInterceptor
where TUserInfo: class, IUserInfo, new()
{
    private static readonly AsyncLocal<InterceptorContext<TUserInfo>> _context = new();

    protected abstract void Initial(IInvocation invocation);
    protected abstract void PreProceed(IInvocation invocation);
    protected abstract void PostProceed(IInvocation invocation);
    protected abstract void OnException(InterceptorExceptionContext context);

    protected DomainContext<TUserInfo> Context;

    public static InterceptorContext<TUserInfo> CurrentContext => _context.Value;
    #region Implementation of IInterceptor

    // 定义一个公共方法Intercept，用于拦截方法调用
    public void Intercept(IInvocation invocation)
    {
        // 调用Initial方法进行初始化操作
        Initial(invocation);
        // 调用PreProceed方法进行预处理操作
        PreProceed(invocation);

        _context.Value = new InterceptorContext<TUserInfo>
        {
            Invocation = invocation,
            DomainContext = Context,
            // 其他上下文信息
        };

        try
        {
            // 执行被拦截的方法
            invocation.Proceed();
        }
        catch (Exception exceptionNeedToBeHandled)
        {
            // 创建一个InterceptorExceptionContext对象，包含方法调用信息和异常信息
            var context = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), exceptionNeedToBeHandled);
            try
            {
                // 调用OnException方法处理异常
                OnException(context);
            }
            catch (Exception exceptionByOnException)
            {
                // 如果OnException方法抛出异常，则抛出InterceptorException
                throw new InterceptorException("Exception raised from OnException() by Interceptor", exceptionByOnException);
            }
            // 如果异常未被处理，则重新抛出异常
            if (!context.ExceptionHandled) throw;
            // 如果不继续执行后续操作，则返回
            if (!context.Continue) return;
        }
        finally
        {
            _context.Value = null;
        }
        // 调用PostProceed方法进行后处理操作
        PostProceed(invocation);
    }

    #endregion
}
public class InterceptorContext<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public IInvocation Invocation { get; set; }
    public DomainContext<TUserInfo> DomainContext { get; set; }
}