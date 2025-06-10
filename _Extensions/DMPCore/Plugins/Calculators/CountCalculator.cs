using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Calculators;
/// <summary>
/// 计数计算器
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="metricName"></param>
public class CountCalculator<T>(string metricName) : IMetricCalculator<T>
    where T : class
{
    public string Name { get; } = metricName;

    public bool IsThreadSafe => true;

    public Dictionary<string, object> Calculate(IEnumerable<T> data)
    {
        return new Dictionary<string, object> { { "value", data.Count() } };
    }
}