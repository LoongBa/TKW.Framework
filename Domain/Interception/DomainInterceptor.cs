using Autofac;
using Castle.DynamicProxy;
using System;
using System.Linq;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 框架级领域拦截器：用于支撑框架级的其它扩展属性
/// </summary>
/// <remarks>TODO:改为链表结构</remarks>
public class DomainInterceptor<TUserInfo> : BaseInterceptor<TUserInfo>, IDisposable
    where TUserInfo: class, IUserInfo, new()
{
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly IDomainGlobalExceptionFactory _DomainGlobalExceptionFactory;
    private readonly ILifetimeScope _LifetimeScope;

    public DomainInterceptor(
        DomainHost<TUserInfo> domainHost,
        IDomainGlobalExceptionFactory domainGlobalExceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(domainHost);
        ArgumentNullException.ThrowIfNull(domainHost.Container);
        _DomainHost = domainHost;
        _LifetimeScope = _DomainHost.Container.BeginLifetimeScope();
        _DomainGlobalExceptionFactory = domainGlobalExceptionFactory;
    }

    #region Overrides of BaseInterceptor

    protected override void Initial(IInvocation invocation)
    {
        Context = _DomainHost.NewDomainContext(invocation, _LifetimeScope);
        Context.EnsureNotNull();
    }

    protected override void PreProceed(IInvocation invocation)
    {
        //TODO: 处理顺序、覆盖顺序
        foreach (var filter in Context!.MethodFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                filter.PreProceed(DomainInvocationWhereType.Method, Context);
        }
        var filters = Context.ControllerFilters.SkipWhile(cf => Context.MethodFilters.Any(mf => mf.TypeId == cf.TypeId));
        foreach (var filter in filters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                filter.PreProceed(DomainInvocationWhereType.Controller, Context);
        }
    }

    protected override void PostProceed(IInvocation invocation)
    {
        //TODO: 处理顺序
        foreach (var filter in Context!.MethodFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                filter.PostProceed(DomainInvocationWhereType.Method, Context);
        }
        var filters = Context.ControllerFilters.SkipWhile(cf => Context.MethodFilters.Any(mf => mf.TypeId == cf.TypeId));
        foreach (var filter in filters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                filter.PreProceed(DomainInvocationWhereType.Controller, Context);
        }
    }

    protected override void OnException(InterceptorExceptionContext context)
    {
        _DomainGlobalExceptionFactory?.HandleException(context);
    }

    #endregion

    #region Implementation of IDisposable

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        _LifetimeScope.Dispose();
    }

    #endregion
}