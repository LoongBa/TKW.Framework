using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 统计引擎接口
/// </summary>
public interface IStatEngine
{
    Task<IEnumerable<FrozenMetricResult>> ExecuteAsync();
}