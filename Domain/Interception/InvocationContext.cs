using System;
using System.Reflection;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// V4 静态 AOP 专用的调用上下文，彻底摆脱 Castle.DynamicProxy
/// </summary>
public sealed class InvocationContext(object proxy, object target, string methodName, object[] arguments)
{
    public object Proxy { get; } = proxy;
    public object Target { get; } = target;
    public string MethodName { get; } = methodName;
    public object[] Arguments { get; } = arguments;

    /// <summary>
    /// 用于存放同步或异步方法的最终返回值
    /// </summary>
    public object? ReturnValue { get; set; }

    // 延迟获取 MethodInfo（因为反射开销大，只有在真正需要时才获取，比如写日志时）
    private MethodInfo? _methodInfo;
    public MethodInfo GetMethodInfo()
    {
        if (_methodInfo == null)
        {
            // 通过目标类型和方法名获取。注意：如果有重载方法，这里可能需要根据参数类型进一步精确匹配
            _methodInfo = Target.GetType().GetMethod(MethodName, BindingFlags.Public | BindingFlags.Instance);
            if (_methodInfo == null) throw new MissingMethodException(Target.GetType().Name, MethodName);
        }
        return _methodInfo;
    }
}