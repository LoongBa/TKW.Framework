using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Preprocessors;

/// <summary>
/// 时间处理预处理器，用于转换和过滤时间相关数据
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class DefaultTimeProcessor<T>(
    Func<T, DateTime?> timeSelector,
    Action<T, DateTime>? timeSetter = null,
    DateTime? startTime = null,
    DateTime? endTime = null)
    : IDataPreprocessor<T>
    where T : class
{
    // 从配置创建处理器
    public static DefaultTimeProcessor<T> CreateFromConfig(MetricConfig config)
    {
        var timeProperty = config.PropertyMap.GetValueOrDefault("TimeField", "Time");
        var startTime = config.PropertyMap.TryGetValue("StartTime", out var startTimeStr)
            ? DateTime.Parse(startTimeStr)
            : (DateTime?)null;

        var endTime = config.PropertyMap.TryGetValue("EndTime", out var endTimeStr)
            ? DateTime.Parse(endTimeStr)
            : (DateTime?)null;

        return new DefaultTimeProcessor<T>(
            timeSelector: PropertyAccessorFactory.Create<T, DateTime?>(timeProperty),
            startTime: startTime,
            endTime: endTime);
    }

    public IEnumerable<T> Process(IEnumerable<T> data)
    {
        foreach (var item in data)
        {
            var time = timeSelector(item);

            // 过滤时间范围
            if (time.HasValue &&
                (startTime.HasValue && time.Value < startTime.Value ||
                 endTime.HasValue && time.Value > endTime.Value))
            {
                continue;
            }

            // 时间标准化（如果需要）
            if (timeSetter != null && time.HasValue)
            {
                timeSetter(item, time.Value);
            }

            yield return item;
        }
    }
}