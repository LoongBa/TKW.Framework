using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Calculators;

public class MetricCalculatorFactory : IMetricCalculatorFactory
{
    /*
           三种注册方式
           方式 1：直接注册类型（最灵活）
           csharp
           factory.RegisterType("repurchase_rate", typeof(RepurchaseRateCalculator<>));
           
           方式 2：泛型注册类型（类型安全）
           csharp
           factory RegisterMethod<Order, RepurchaseRateCalculator<Order>>("repurchase_rate");
           
           方式 3：委托注册（自定义逻辑）
           csharp
           factory RegisterMethod<Order>("repurchase_rate", config => new RepurchaseRateCalculator<Order>(...));
           
           使用示例
           csharp
           // 创建工厂
           var factory = new MetricCalculatorFactory();
           
           // 方式1：直接注册类型
           factory.RegisterType("repurchase_rate", typeof(RepurchaseRateCalculator<>));
           
           // 方式2：泛型注册类型
           factory RegisterMethod<Order, RepurchaseRateCalculator<Order>>("repurchase_rate");
           
           // 方式3：委托注册
           factory RegisterMethod<Order>("average", config => {
               var valueField = config.PropertyMap.GetValueOrDefault("ValueField", "Amount");
               return new AverageCalculator<Order>(config.Name, valueField);
           });
           
           // 创建计算器
           var calculator = factory.Create<Order>("repurchase_rate", config);
           
           总结
           通过恢复 RegisterType 方法，我们确保了工厂类的完整性，支持三种不同的注册方式，满足各种使用场景的需求。这种设计既保持了灵活性，又提供了类型安全的选项，是一个更健壮的实现。         
         */

    private readonly Dictionary<string, Type> _TypeRegistrations = [];
    private readonly Dictionary<string, Delegate> _DelegateRegistrations = [];

    // 基础类型注册方法（直接注册类型）
    public void RegisterType(string calculatorType, Type calculatorImplementationType)
    {
        _TypeRegistrations[calculatorType] = calculatorImplementationType;
    }

    // 泛型类型注册方法（简化类型参数）
    public void RegisterMethod<T, TCalculator>(string calculatorType)
        where T : class
        where TCalculator : IMetricCalculator<T>
    {
        RegisterType(calculatorType, typeof(TCalculator));
    }

    // 委托注册方法
    public void RegisterMethod<T>(string calculatorType, Func<MetricConfig, IMetricCalculator<T>> factoryMethod)
        where T : class
    {
        _DelegateRegistrations[calculatorType] = factoryMethod;
    }

    // 创建计算器实例
    public IMetricCalculator<T> Create<T>(string calculatorType, MetricConfig config) where T : class
    {
        // 优先使用委托注册
        if (_DelegateRegistrations.TryGetValue(calculatorType, out var factoryDelegate))
        {
            var typedDelegate = factoryDelegate as Func<MetricConfig, IMetricCalculator<T>>;
            return typedDelegate?.Invoke(config)
                   ?? throw new InvalidOperationException($"委托工厂类型不匹配: {calculatorType}");
        }

        // 其次使用类型注册
        if (_TypeRegistrations.TryGetValue(calculatorType, out var implementationType))
        {
            var genericType = implementationType.MakeGenericType(typeof(T));
            try
            {
                return (IMetricCalculator<T>)Activator.CreateInstance(
                    genericType,
                    config.Name,
                    config.PropertyMap);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法创建计算器实例: {calculatorType}", ex);
            }
        }

        throw new NotSupportedException($"未注册的计算器类型: {calculatorType}");
    }
}