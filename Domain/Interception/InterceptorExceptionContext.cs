using System;

namespace TKW.Framework.Domain.Interception {
    public class InterceptorExceptionContext
    {
        public InterceptorExceptionContext(DomainMethodInvocation invocation, Exception exception)
        {
            Invocation = invocation;
            Exception = exception;
        }
        public Exception Exception { get; }
        public DomainMethodInvocation Invocation { get; }

        public bool Continue { get; set; }
        public bool ExceptionHandled { get; set; }
    }
}