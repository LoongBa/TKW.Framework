using Castle.DynamicProxy;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 拦截器上下文
/// </summary>
public class InterceptorContext<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public required IInvocation Invocation { get; init; }
    public required DomainContext<TUserInfo>? DomainContext { get; init; }
}