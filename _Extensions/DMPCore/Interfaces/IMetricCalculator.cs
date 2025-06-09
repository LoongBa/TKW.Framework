namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 指标计算插件接口
/// </summary>
public interface IMetricCalculator
{
    string Name { get; }
    bool IsThreadSafe { get; }
    Dictionary<string, object> Calculate(IEnumerable<Dictionary<string, object>> groupedData);
}