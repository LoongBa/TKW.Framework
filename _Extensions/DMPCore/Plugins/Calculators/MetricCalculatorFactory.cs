using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Calculators;

/// <summary>
/// 计算器工厂（从 StatEngine 获取注册类型并实例化）
/// </summary>
public class MetricCalculatorFactory(IStatEngine statEngine) : IMetricCalculatorFactory
{
    private readonly IStatEngine _statEngine = statEngine ?? throw new ArgumentNullException(nameof(statEngine));

    public IMetricCalculator<T> Create<T>(string calculatorType) where T : class
    {
        // 从 StatEngine 查询注册的计算器类型
        var calculatorTypeInfo = _statEngine.GetRegisteredType<IMetricCalculator<T>>(calculatorType);
        if (calculatorTypeInfo == null)
        {
            throw new InvalidOperationException($"未找到计算器类型: {calculatorType}（目标接口: IMetricCalculator<{typeof(T).Name}>）");
        }

        // 实例化计算器（支持带配置的构造函数）
        try
        {
            return (IMetricCalculator<T>)Activator.CreateInstance(
                calculatorTypeInfo,
                _statEngine.GetProcessingConfig().MetricConfig // 从 StatEngine 获取计算配置
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"实例化计算器 {calculatorType} 失败", ex);
        }
    }
}