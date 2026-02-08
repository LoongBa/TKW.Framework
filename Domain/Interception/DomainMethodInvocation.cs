using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace TKW.Framework.Domain.Interception;

public sealed class DomainMethodInvocation(IInvocation invocation)
{
    private readonly IInvocation _Invocation = invocation;

    public object GetArgumentValue(int index) => _Invocation.GetArgumentValue(index);
    public MethodInfo GetConcreteMethod() => _Invocation.GetConcreteMethod();
    public MethodInfo GetConcreteMethodInvocationTarget() => _Invocation.GetConcreteMethodInvocationTarget();
    public object InvocationTarget => _Invocation.InvocationTarget;
    public MethodInfo Method => _Invocation.Method;
    public MethodInfo MethodInvocationTarget => _Invocation.MethodInvocationTarget;
    public object Proxy => _Invocation.Proxy;
    public object ReturnValue
    {
        get => _Invocation.ReturnValue;
        set => _Invocation.ReturnValue = value;
    }
    public Type TargetType => _Invocation.TargetType;

    public void SetArgumentValue(int index, object value) => _Invocation.SetArgumentValue(index, value);
    public object[] Arguments => _Invocation.Arguments;
    public Type[] GenericArguments => _Invocation.GenericArguments;
}