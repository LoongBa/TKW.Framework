using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace TKW.Framework.Domain.Interception;

public sealed class DomainMethodInvocation(IInvocation invocation)
{
    private readonly IInvocation _invocation = invocation;

    public object GetArgumentValue(int index) => _invocation.GetArgumentValue(index);
    public MethodInfo GetConcreteMethod() => _invocation.GetConcreteMethod();
    public MethodInfo GetConcreteMethodInvocationTarget() => _invocation.GetConcreteMethodInvocationTarget();
    public object InvocationTarget => _invocation.InvocationTarget;
    public MethodInfo Method => _invocation.Method;
    public MethodInfo MethodInvocationTarget => _invocation.MethodInvocationTarget;
    public object Proxy => _invocation.Proxy;
    public object ReturnValue
    {
        get => _invocation.ReturnValue;
        set => _invocation.ReturnValue = value;
    }
    public Type TargetType => _invocation.TargetType;

    public void SetArgumentValue(int index, object value) => _invocation.SetArgumentValue(index, value);
    public object[] Arguments => _invocation.Arguments;
    public Type[] GenericArguments => _invocation.GenericArguments;
}