using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Preprocessors;

/// <summary>
/// 预处理器工厂（从 StatEngine 获取注册类型并实例化）
/// </summary>
public class PreprocessorFactory : IPreprocessorFactory
{
    private readonly IStatEngine _statEngine;

    public PreprocessorFactory(IStatEngine statEngine)
    {
        _statEngine = statEngine ?? throw new ArgumentNullException(nameof(statEngine));
    }

    public IDataPreprocessor<T> Create<T>(string preprocessorName) where T : class
    {
        // 从 StatEngine 查询注册的预处理器类型
        var preprocessorTypeInfo = _statEngine.GetRegisteredType<IDataPreprocessor<T>>(preprocessorName);
        if (preprocessorTypeInfo == null)
        {
            throw new InvalidOperationException($"未找到预处理器: {preprocessorName}（目标接口: IDataPreprocessor<{typeof(T).Name}>）");
        }

        // 实例化预处理器（支持带配置的构造函数）
        try
        {
            return (IDataPreprocessor<T>)Activator.CreateInstance(
                preprocessorTypeInfo,
                _statEngine.GetProcessingConfig().MetricConfig // 从 StatEngine 获取处理配置
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"实例化预处理器 {preprocessorName} 失败", ex);
        }
    }
}