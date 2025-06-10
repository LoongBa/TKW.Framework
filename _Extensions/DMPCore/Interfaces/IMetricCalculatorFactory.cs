namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 计算器工厂接口（负责创建 IMetricCalculator 实例）
/// </summary>
public interface IMetricCalculatorFactory
{
    /// <summary>
    /// 创建指定类型的计算器实例
    /// </summary>
    /// <typeparam name="T">输入数据类型</typeparam>
    /// <param name="calculatorType">计算器类型名称（如 "Average", "Sum"）</param>
    /// <returns>IMetricCalculator<T> 实例</returns>
    IMetricCalculator<T> Create<T>(string calculatorType) where T : class;
}