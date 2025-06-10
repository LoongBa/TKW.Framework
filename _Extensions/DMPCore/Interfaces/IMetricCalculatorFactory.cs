using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

public interface IMetricCalculatorFactory
{
    IMetricCalculator<T> Create<T>(string calculatorType, MetricConfig config) where T : class;
    void RegisterType(string calculatorType, Type type);
    void RegisterMethod<T>(string calculatorType, Func<MetricConfig, IMetricCalculator<T>> factoryMethod) where T : class;
}