namespace TKWF.DMP.Core.Interfaces;
/// <summary>
/// 指标计算插件接口
/// </summary>
public interface IMetricCalculator<in T> where T : class
{
    string Name { get; }
    bool IsThreadSafe { get; }
    Dictionary<string, object> Calculate(IEnumerable<T> groupedData);
}