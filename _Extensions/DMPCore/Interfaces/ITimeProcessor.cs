using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 时间处理接口
/// </summary>
public interface ITimeProcessor
{
    List<TimeRange> GetIntervals(DateTime startTime, DateTime endTime, string frequency);
}