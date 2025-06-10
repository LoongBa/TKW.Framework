using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;
/// <summary>
/// 时间处理接口
/// </summary>
public interface ITimeProcessor : IPreprocessor
{
    List<TimeRange> GetIntervals(DateTime startTime, DateTime endTime, string frequency);
}