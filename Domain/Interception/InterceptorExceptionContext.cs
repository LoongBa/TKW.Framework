using System;

namespace TKW.Framework.Domain.Interception {
    public class InterceptorExceptionContext(DomainMethodInvocation invocation, Exception exception)
    {
        public Exception Exception { get; } = exception;
        public DomainMethodInvocation Invocation { get; } = invocation;

        public bool Continue { get; set; }
        public bool ExceptionHandled { get; set; }
    }
}