using System;
using System.Linq;
using Autofac;
using Castle.DynamicProxy;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Interception
{
    /// <summary>
    /// 框架级领域拦截器：用于支撑框架级的其它扩展属性
    /// </summary>
    /// <remarks>TODO:改为链表结构</remarks>
    public class DomainInterceptor : BaseInterceptor, IDisposable
    {
        private readonly DomainHost _DomainHost;
        private readonly IDomainGlobalExceptionFactory _DomainGlobalExceptionFactory;
        private DomainContext _Context;
        private readonly ILifetimeScope _LifetimeScope;

        public DomainInterceptor(
            DomainHost domainHost,
            IDomainGlobalExceptionFactory domainGlobalExceptionFactory = null)
        {
            domainHost.AssertNotNull(name: nameof(domainHost));
            _DomainHost = domainHost;
            _LifetimeScope = _DomainHost.Container.BeginLifetimeScope();
            _DomainGlobalExceptionFactory = domainGlobalExceptionFactory;
        }

        #region Overrides of BaseInterceptor

        protected override void Initial(IInvocation invocation)
        {
            _Context = _DomainHost.NewDomainContext(invocation, _LifetimeScope);
        }

        protected override void PreProceed(IInvocation invocation)
        {
            //TODO: 处理顺序、覆盖顺序
            foreach (var filter in _Context.MethodFilters)
            {
                if (filter.CanWeGo(DomainInvocationWhereType.Method, _Context))
                    filter.PreProceed(DomainInvocationWhereType.Method, _Context);
            }
            var filters = _Context.ControllerFilters.SkipWhile(cf => _Context.MethodFilters.Any(mf => mf.TypeId == cf.TypeId));
            foreach (var filter in filters)
            {
                if (filter.CanWeGo(DomainInvocationWhereType.Controller, _Context))
                    filter.PreProceed(DomainInvocationWhereType.Controller, _Context);
            }
        }

        protected override void PostProceed(IInvocation invocation)
        {
            //TODO: 处理顺序
            foreach (var filter in _Context.MethodFilters)
            {
                if (filter.CanWeGo(DomainInvocationWhereType.Method, _Context))
                    filter.PostProceed(DomainInvocationWhereType.Method, _Context);
            }
            var filters = _Context.ControllerFilters.SkipWhile(cf => _Context.MethodFilters.Any(mf => mf.TypeId == cf.TypeId));
            foreach (var filter in filters)
            {
                if (filter.CanWeGo(DomainInvocationWhereType.Controller, _Context))
                    filter.PreProceed(DomainInvocationWhereType.Controller, _Context);
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
}