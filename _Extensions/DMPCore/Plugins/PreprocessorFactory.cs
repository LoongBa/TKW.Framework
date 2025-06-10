using System.Collections.Concurrent;
using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;
using TKWF.DMP.Core.Plugins.Preprocessors;

namespace TKWF.DMP.Core.Plugins;
using System;
using System.Collections.Concurrent;

public static class PreprocessorFactory
{
    private static readonly ConcurrentDictionary<string, Func<MetricConfig, object>> _factories = new();

    static PreprocessorFactory()
    {
        // 注册内置预处理器
        RegisterMethod("total_price", TotalPricePreprocessor<object>.CreateFromConfig);
        RegisterMethod("filter", FilterPreprocessor<object>.CreateFromConfig);
    }

    // 注册预处理器工厂方法（泛型）
    public static void RegisterMethod<T>(string name, Func<MetricConfig, IDataPreprocessor<T>> factory)
        where T : class
    {
        _factories[name] = factory;
    }

    // 注册预处理器工厂方法（非泛型）
    public static void RegisterMethod(string name, Func<MetricConfig, IPreprocessor> factory)
    {
        _factories[name] = config => new PreprocessorAdapter<object>(factory(config));
    }

    // 创建预处理器（泛型）
    public static IDataPreprocessor<T> Create<T>(string name, MetricConfig config)
        where T : class
    {
        if (_factories.TryGetValue(name, out var factory))
        {
            var preprocessor = factory(config);

            if (preprocessor is IDataPreprocessor<T> genericPreprocessor)
                return genericPreprocessor;

            if (preprocessor is IPreprocessor nonGenericPreprocessor)
                return new PreprocessorAdapter<T>(nonGenericPreprocessor);

            throw new InvalidOperationException($"预处理器类型不匹配: {name}");
        }

        throw new NotSupportedException($"未注册的预处理器: {name}");
    }

    // 创建预处理器（非泛型）
    public static IPreprocessor Create(string name, MetricConfig config)
    {
        if (_factories.TryGetValue(name, out var factory))
        {
            return factory(config) as IPreprocessor
                   ?? throw new InvalidOperationException($"预处理器类型不匹配: {name}");
        }

        throw new NotSupportedException($"未注册的预处理器: {name}");
    }
}