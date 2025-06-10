using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;
using TKWF.DMP.Core.Plugins.Converters;
using TKWF.DMP.Core.Plugins.Preprocessors;

namespace TKWF.DMP.Core;

/// <summary>
/// 统计引擎，负责协调数据预处理器和指标计算器
/// </summary>
public class StatEngine : IStatEngine
{
    #region 私有字段

    private readonly Dictionary<(Type dataType, string name), object> _Preprocessors = [];

    private readonly Dictionary<string, Func<MetricConfig, object>> _PreprocessorFactories = [];

    // 确保 _calculatorFactory 的类型已正确声明并初始化
    private readonly IMetricCalculatorFactory _CalculatorFactory;

    #endregion

    #region 构造函数

    public StatEngine(IMetricCalculatorFactory calculatorFactory)
    {
        _CalculatorFactory = calculatorFactory;

        // 注册内置预处理器工厂
        RegisterMethod("time_processor", DefaultTimeProcessor<object>.CreateFromConfig);
        RegisterMethod("data_converter", DefaultDataConverter<object>.CreateFromConfig);
        RegisterMethod("total_price", TotalPricePreprocessor<object>.CreateFromConfig);
    }

    #endregion

    #region 预处理器注册方法

    /// <summary>
    /// 注册预处理器实例（泛型方式）
    /// </summary>
    public void RegisterPreprocessor<T>(string name, IDataPreprocessor<T> preprocessor)
        where T : class
    {
        _Preprocessors[(typeof(T), name)] = preprocessor;
    }

    /// <summary>
    /// 注册预处理器实例（非泛型方式）
    /// </summary>
    public void RegisterPreprocessor(string name, IPreprocessor preprocessor)
    {
        _Preprocessors[(typeof(object), name)] = new PreprocessorAdapter<object>(preprocessor);
    }

    /// <summary>
    /// 注册预处理器工厂方法（泛型）
    /// </summary>
    public void RegisterMethod<T>(string name, Func<MetricConfig, IDataPreprocessor<T>> factoryMethod)
        where T : class
    {
        _PreprocessorFactories[name] = factoryMethod;
    }

    /// <summary>
    /// 注册预处理器工厂方法（非泛型）
    /// </summary>
    public void RegisterMethod(string name, Func<MetricConfig, IPreprocessor> factoryMethod)
    {
        _PreprocessorFactories[name] = config => new PreprocessorAdapter<object>(factoryMethod(config));
    }

    #endregion

    #region 计算器注册方法

    /// <summary>
    /// 注册计算器工厂方法
    /// </summary>
    public void RegisterMethod<T>(string calculatorType, Func<MetricConfig, IMetricCalculator<T>> factoryMethod)
        where T : class
    {
        _CalculatorFactory.RegisterMethod(calculatorType, factoryMethod);
    }

    #endregion

    #region 统计计算方法

    /// <summary>
    /// 执行统计计算
    /// </summary>
    public Dictionary<string, object> Calculate<T>(string calculatorType, IEnumerable<T> data, MetricConfig? config = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(data);

        config ??= new MetricConfig { CalculatorType = calculatorType };

        // 应用预处理器链
        var processedData = ApplyPreprocessors(data, config);

        // 创建并执行计算器
        var calculator = _CalculatorFactory.Create<T>(calculatorType, config);
        return calculator.Calculate(processedData);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 应用预处理器链
    /// </summary>
    private IEnumerable<T> ApplyPreprocessors<T>(IEnumerable<T> data, MetricConfig config)
        where T : class
    {
        if (config.PreprocessorNames.Count == 0)
            return data;

        var processed = data;

        foreach (var name in config.PreprocessorNames)
        {
            // 尝试从已注册的预处理器实例获取
            if (_Preprocessors.TryGetValue((typeof(T), name), out var preprocessorObj))
            {
                if (preprocessorObj is IDataPreprocessor<T> preprocessor)
                {
                    processed = preprocessor.Process(processed);
                    continue;
                }
            }

            // 尝试从预处理器工厂创建
            if (_PreprocessorFactories.TryGetValue(name, out var factory))
            {
                try
                {
                    var factoryPreprocessor = factory(config);

                    if (factoryPreprocessor is IDataPreprocessor<T> genericPreprocessor)
                    {
                        processed = genericPreprocessor.Process(processed);
                    }
                    else if (factoryPreprocessor is IPreprocessor nonGenericPreprocessor)
                    {
                        processed = new PreprocessorAdapter<T>(nonGenericPreprocessor).Process(processed);
                    }
                    else
                    {
                        throw new InvalidOperationException($"预处理器类型不匹配: {name}");
                    }

                    continue;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"创建预处理器失败: {name}", ex);
                }
            }

            throw new NotSupportedException($"未注册的预处理器: {name}");
        }

        return processed;
    }

    #endregion
}