using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 统计引擎接口
/// </summary>
public interface IStatEngine
{
    // 预处理器注册方法
    void RegisterPreprocessor<T>(string name, IDataPreprocessor<T> preprocessor) where T : class;
    void RegisterPreprocessor(string name, IPreprocessor preprocessor);
    void RegisterMethod<T>(string name, Func<MetricConfig, IDataPreprocessor<T>> factoryMethod) where T : class;
    void RegisterMethod(string name, Func<MetricConfig, IPreprocessor> factoryMethod);

    // 计算器注册方法
    void RegisterMethod<T>(string calculatorType, Func<MetricConfig, IMetricCalculator<T>> factoryMethod) where T : class;

    // 执行统计计算
    Dictionary<string, object> Calculate<T>(string calculatorType, IEnumerable<T> data, MetricConfig? config = null) where T : class;
}