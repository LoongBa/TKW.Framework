namespace TKWF.DMP.Core.Models;
/// <summary>
/// 时间配置
/// </summary>
public class TimeConfig
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Frequency { get; set; }
    public string TargetTimezone { get; set; }
    public string WeekStart { get; set; }
}