using System.Globalization;
using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Preprocessores;
/// <summary>
/// 时间处理器实现
/// </summary>
public class DefaultTimeProcessor : ITimeProcessor
{
    private readonly TimeZoneInfo _targetTimezone;

    public DefaultTimeProcessor(string targetTimezone)
    {
        try
        {
            _targetTimezone = TimeZoneInfo.FindSystemTimeZoneById(targetTimezone);
        }
        catch (TimeZoneNotFoundException)
        {
            _targetTimezone = TimeZoneInfo.Local;
            Console.WriteLine($"时区 {targetTimezone} 不存在，使用本地时区");
        }
    }

    public List<TimeRange> GetIntervals(DateTime startTime, DateTime endTime, string frequency)
    {
        var convertedStart = TimeZoneInfo.ConvertTime(startTime, TimeZoneInfo.Utc, _targetTimezone);
        var convertedEnd = TimeZoneInfo.ConvertTime(endTime, TimeZoneInfo.Utc, _targetTimezone);

        var intervals = new List<TimeRange>();
        var currentStart = convertedStart;

        while (currentStart < convertedEnd)
        {
            DateTime currentEnd;
            switch (frequency.ToLower())
            {
                case "day":
                    currentEnd = currentStart.AddDays(1);
                    break;
                case "week":
                    var startOfWeek = GetStartOfWeek(currentStart);
                    currentEnd = startOfWeek.AddDays(7);
                    break;
                case "month":
                    currentEnd = new DateTime(currentStart.Year, currentStart.Month, 1).AddMonths(1);
                    if (currentEnd > convertedEnd)
                        currentEnd = convertedEnd;
                    break;
                default:
                    throw new NotSupportedException($"不支持的时间频次: {frequency}");
            }

            intervals.Add(new TimeRange(currentStart, currentEnd));
            currentStart = currentEnd;
        }

        return intervals;
    }

    private DateTime GetStartOfWeek(DateTime date)
    {
        var culture = CultureInfo.CurrentCulture;
        var dayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
        var diff = date.DayOfWeek - dayOfWeek;
        if (diff < 0)
            diff += 7;
        return date.AddDays(-diff).Date;
    }
}