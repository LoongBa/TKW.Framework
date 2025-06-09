using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;
/// <summary>
/// 结果输出插件接口
/// </summary>
public interface IResultExporter
{
    string TargetType { get; }
    void Export(IEnumerable<FrozenMetricResult> results);
}