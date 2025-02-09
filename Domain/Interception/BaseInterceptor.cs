using System;
using Castle.DynamicProxy;

namespace TKW.Framework.Domain.Interception
{
    public abstract class BaseInterceptor : IInterceptor
    {
        protected abstract void Initial(IInvocation invocation);
        protected abstract void PreProceed(IInvocation invocation);
        protected abstract void PostProceed(IInvocation invocation);
        protected abstract void OnException(InterceptorExceptionContext context);

        #region Implementation of IInterceptor

        public void Intercept(IInvocation invocation)
        {
            Initial(invocation);
            PreProceed(invocation);
            try
            {
                invocation.Proceed();
            }
            catch (Exception exceptionNeedToBeHandled)
            {
                var context = new InterceptorExceptionContext(new DomainMethodInvocation(invocation), exceptionNeedToBeHandled);
                try
                {
                    OnException(context);
                }
                catch (Exception exceptionByOnException)
                {
                    throw new InterceptorException("Exception raised from OnException() by Interceptor", exceptionByOnException);
                }
                if (!context.ExceptionHandled) throw;
                if (!context.Continue) return;
            }
            PostProceed(invocation);
        }

        #endregion
    }
}