using System;
using System.Collections.ObjectModel;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace TKW.Framework.Domain.Interception
{
    public sealed class DomainContext
    {
        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public DomainContext(
            DomainUser domainUser,
            IInvocation invocation,
            DomainContracts contracts,
            ILoggerFactory loggerFactory = null)
        {
            DomainUser = domainUser;
            Invocation = new DomainMethodInvocation(invocation);
            MethodFilters = contracts.MethodFilters.AsReadOnly();
            ControllerFilters = contracts.ControllerFilters.AsReadOnly();
            MethodFlags = contracts.MethodFlags.AsReadOnly();
            ControllerFlags = contracts.ControllerFlags.AsReadOnly();
            Logger = loggerFactory?.CreateLogger($"{invocation.TargetType.Name}.{invocation.Method.Name}()");
        }
        public ILogger Logger { get; }
        public DomainUser DomainUser { get; }
        public DomainMethodInvocation Invocation { get; }

        internal ReadOnlyCollection<DomainActionFilterAttribute> MethodFilters { get; }

        internal ReadOnlyCollection<DomainActionFilterAttribute> ControllerFilters { get; }
        public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; }

        public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; }
    }

    public sealed class DomainMethodInvocation
    {
        private readonly IInvocation _Invocation;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public DomainMethodInvocation(IInvocation invocation)
        {
            _Invocation = invocation;
        }

        /// <summary>
        ///   Gets the value of the argument at the specified <paramref name="index" />.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the argument at the specified <paramref name="index" />.</returns>
        public object GetArgumentValue(int index)
        {
            return _Invocation.GetArgumentValue(index);
        }

        /// <summary>
        ///   Returns the concrete instantiation of the <see cref="P:Castle.DynamicProxy.IInvocation.Method" /> on the proxy, with any generic
        ///   parameters bound to real types.
        /// </summary>
        /// <returns>
        ///   The concrete instantiation of the <see cref="P:Castle.DynamicProxy.IInvocation.Method" /> on the proxy, or the <see cref="P:Castle.DynamicProxy.IInvocation.Method" /> if
        ///   not a generic method.
        /// </returns>
        /// <remarks>
        ///   Can be slower than calling <see cref="P:Castle.DynamicProxy.IInvocation.Method" />.
        /// </remarks>
        public MethodInfo GetConcreteMethod()
        {
            return _Invocation.GetConcreteMethod();
        }

        /// <summary>
        ///   Returns the concrete instantiation of <see cref="P:Castle.DynamicProxy.IInvocation.MethodInvocationTarget" />, with any
        ///   generic parameters bound to real types.
        ///   For interface proxies, this will point to the <see cref="T:System.Reflection.MethodInfo" /> on the target class.
        /// </summary>
        /// <returns>The concrete instantiation of <see cref="P:Castle.DynamicProxy.IInvocation.MethodInvocationTarget" />, or
        /// <see cref="P:Castle.DynamicProxy.IInvocation.MethodInvocationTarget" /> if not a generic method.</returns>
        /// <remarks>
        ///   In debug builds this can be slower than calling <see cref="P:Castle.DynamicProxy.IInvocation.MethodInvocationTarget" />.
        /// </remarks>
        public MethodInfo GetConcreteMethodInvocationTarget()
        {
            return _Invocation.GetConcreteMethodInvocationTarget();
        }

        /// <summary>
        ///   Overrides the value of an argument at the given <paramref name="index" /> with the
        ///   new <paramref name="value" /> provided.
        /// </summary>
        /// <remarks>
        ///   This method accepts an <see cref="T:System.Object" />, however the value provided must be compatible
        ///   with the type of the argument defined on the method, otherwise an exception will be thrown.
        /// </remarks>
        /// <param name="index">The index of the argument to override.</param>
        /// <param name="value">The new value for the argument.</param>
        public void SetArgumentValue(int index, object value)
        {
            _Invocation.SetArgumentValue(index, value);
        }

        /// <summary>
        ///   Gets the arguments that the <see cref="P:Castle.DynamicProxy.IInvocation.Method" /> has been invoked with.
        /// </summary>
        /// <value>The arguments the method was invoked with.</value>
        public object[] Arguments => _Invocation.Arguments;

        /// <summary>Gets the generic arguments of the method.</summary>
        /// <value>The generic arguments, or null if not a generic method.</value>
        public Type[] GenericArguments => _Invocation.GenericArguments;

        /// <summary>
        ///   Gets the object on which the invocation is performed. This is different from proxy object
        ///   because most of the time this will be the proxy target object.
        /// </summary>
        /// <seealso cref="T:Castle.DynamicProxy.IChangeProxyTarget" />
        /// <value>The invocation target.</value>
        public object InvocationTarget => _Invocation.InvocationTarget;

        /// <summary>
        ///   Gets the <see cref="T:System.Reflection.MethodInfo" /> representing the method being invoked on the proxy.
        /// </summary>
        /// <value>The <see cref="T:System.Reflection.MethodInfo" /> representing the method being invoked.</value>
        public MethodInfo Method => _Invocation.Method;

        /// <summary>
        ///   For interface proxies, this will point to the <see cref="T:System.Reflection.MethodInfo" /> on the target class.
        /// </summary>
        /// <value>The method invocation target.</value>
        public MethodInfo MethodInvocationTarget => _Invocation.MethodInvocationTarget;

        /// <summary>Gets or sets the return value of the method.</summary>
        /// <value>The return value of the method.</value>
        public object ReturnValue
        {
            get => _Invocation.ReturnValue;
            set => _Invocation.ReturnValue = value;
        }

        /// <summary>
        ///   Gets the type of the target object for the intercepted method.
        /// </summary>
        /// <value>The type of the target object.</value>
        public Type TargetType => _Invocation.TargetType;

    }
}