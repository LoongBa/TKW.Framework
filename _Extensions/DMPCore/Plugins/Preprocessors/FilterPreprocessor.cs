using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Preprocessors;

public class FilterPreprocessor<T>(Func<T, bool> predicate) : IDataPreprocessor<T>
    where T : class
{
    public static FilterPreprocessor<T> CreateFromConfig(MetricConfig config)
    {
        if (!config.PropertyMap.TryGetValue("FilterExpression", out var expression))
            throw new ArgumentException("过滤预处理器需要配置 FilterExpression");

        // 这里可以使用表达式树解析字符串表达式
        // 简化示例，实际应使用更健壮的表达式解析
        bool Predicate(T x) => true;

        return new FilterPreprocessor<T>(Predicate);
    }

    public IEnumerable<T> Process(IEnumerable<T> data)
    {
        return data.Where(predicate);
    }
}