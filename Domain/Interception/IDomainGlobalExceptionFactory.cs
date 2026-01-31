namespace TKW.Framework.Domain.Interception;

public interface IDomainGlobalExceptionFactory {
    void HandleException(InterceptorExceptionContext context);
}