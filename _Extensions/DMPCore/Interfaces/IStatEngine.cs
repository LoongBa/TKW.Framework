using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;
/// <summary>
/// 统计引擎接口
/// </summary>
public interface IStatEngine
{
    Task<IEnumerable<FrozenMetricResult>> ExecuteAsync();
}