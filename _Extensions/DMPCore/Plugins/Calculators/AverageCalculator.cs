using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Calculators;
/// <summary>
/// 平均值计算器（泛型实现）
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TValue"></typeparam>
/// <param name="metricName"></param>
/// <param name="valueSelector"></param>
public class AverageCalculator<T, TValue>(string metricName, Func<T, TValue> valueSelector) : IMetricCalculator<T>
    where T : class
    where TValue : struct, IComparable, IConvertible
{
    public string Name { get; } = metricName;

    public bool IsThreadSafe => true;

    public Dictionary<string, object> Calculate(IEnumerable<T> data)
    {
        var enumerable = data as T[] ?? data.ToArray();
        if (!enumerable.Any())
            return new Dictionary<string, object> { { "value", 0 } };

        var sum = enumerable.Sum(x => Convert.ToDouble(valueSelector(x)));
        var count = enumerable.Count();

        return new Dictionary<string, object> { { "value", sum / count } };
    }
}