using System;

namespace TKW.Framework.Common.DataType
{
    /// <summary>
    /// 日期区间
    /// </summary>
    public struct DateTimeRange
    {
        /// <summary>
        /// 结束于
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// 开始于
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public static DateTimeRange Empty => new DateTimeRange(DateTime.MinValue, DateTime.MaxValue);

        /// <summary>
        /// 以开始日期和结束日期初始化DateTimeRange实例
        /// </summary>
        /// <param name="start">开始日期</param>
        /// <param name="end">结束日期</param>
        public DateTimeRange(DateTime start, DateTime end)
        {
            if (start >= end) throw new ArgumentOutOfRangeException($"结束时间必须晚于开始时间");

            Start = start;
            End = end;
        }

        /// <summary>
        /// 以开始和结束的年、月、日初始化DateTimeRange实例
        /// </summary>
        /// <param name="startYear">开始日期 年</param>
        /// <param name="startMonth">开始日期 月</param>
        /// <param name="startDay">开始日期 日</param>
        /// <param name="endYear">结束日期 年</param>
        /// <param name="endMonth">结束日期 月</param>
        /// <param name="endDay">结束日期 日</param>
        public DateTimeRange(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
            : this(new DateTime(startYear, startMonth, startDay, 0, 0, 0, 0), new DateTime(endYear, endMonth, endDay, 23, 59, 59, 999)) //TODO: 将 End 改为 0:0:0
        {
        }

        /// <summary>
        /// 指定开始日期初始化DateTimeRange实例，无结束日期
        /// </summary>
        /// <param name="start">开始日期</param>
        public DateTimeRange(DateTime start)
            : this(start, DateTime.MaxValue)
        {
        }

        /// <summary>
        /// 指定开始日期的年、月、日初始化DateTimeRange实例，无结束日期
        /// </summary>
        /// <param name="startYear">开始日期 年</param>
        /// <param name="startMonth">开始日期 月</param>
        /// <param name="startDay">开始日期 日</param>
        public DateTimeRange(int startYear, int startMonth, int startDay)
            : this(new DateTime(startYear, startMonth, startDay, 0, 0, 0, 0), DateTime.MaxValue)
        {
        }

        /// <summary>
        /// 是否有开始值（不为默认值：DateTime.MaxValue、DateTime.MinValue）
        /// </summary>
        public bool HasStartDateTime => Start != DateTime.MaxValue && Start != DateTime.MinValue;
        /// <summary>
        /// 是否有结束值（不为默认值：DateTime.MaxValue、DateTime.MinValue）
        /// </summary>
        public bool HasEndDateTime => End != DateTime.MaxValue && End != DateTime.MinValue;

        /// <summary>
        /// 区间间隔
        /// </summary>
        public TimeSpan TimeSpan()
        {
            return End - Start;
        }

        /// <summary>
        /// 指定的时间是否在范围内：前闭后开
        /// </summary>
        public bool IsInRange(DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue) return false;
            return dateTime >= Start && dateTime < End;
        }

        public static DateTime Tomorrow => DateTime.Now.Date.AddDays(1);

        public static DateTime ThisYearStartDate => new DateTime(DateTime.Now.Year, 1, 1);

        /// <summary>
        /// 本年
        /// </summary>
        public static DateTimeRange ThisYear => new DateTimeRange(ThisYearStartDate, ThisYearStartDate.AddYears(1));

        public static DateTimeRange ThisYear2Today => new DateTimeRange(ThisYearStartDate, Tomorrow);

        public static DateTime ThisQuarterStartDate
        {
            get
            {
                var now = DateTime.Now;
                var month = now.Month;
                var quarter = month / 3;
                if (quarter > 0 && month % 3 == 0) quarter--;
                return new DateTime(now.Year, quarter * 3 + 1, 1);
            }
        }

        public static DateTime ThisQuarterEndDate => new DateTime(ThisQuarterStartDate.Year, ThisQuarterStartDate.Month + 3, 1);

        /// <summary>
        /// 本季度
        /// </summary>
        public static DateTimeRange ThisQuarter => new DateTimeRange(ThisQuarterStartDate, ThisQuarterEndDate);

        public static DateTimeRange ThisQuarter2Today => new DateTimeRange(ThisQuarterStartDate, Tomorrow);

        public static DateTime ThisMonthStartDate => new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        /// <summary>
        /// 本月
        /// </summary>
        public static DateTimeRange ThisMonth => new DateTimeRange(ThisMonthStartDate, ThisMonthStartDate.AddMonths(1));

        public static DateTimeRange ThisMonth2Today => new DateTimeRange(ThisMonthStartDate, Tomorrow);

        public static DateTime ThisWeekStartDate => DateTime.Now.Date.AddDays(-(double)(DateTime.Now.DayOfWeek - 1));

        /// <summary>
        /// 本周
        /// </summary>
        public static DateTimeRange ThisWeek => new DateTimeRange(ThisWeekStartDate, ThisWeekStartDate.AddDays(7));

        public static DateTimeRange ThisWeek2Today => new DateTimeRange(ThisWeekStartDate, Tomorrow);

        /// <summary>
        /// 过去30天
        /// </summary>
        public static DateTimeRange Last30Days
        {
            get
            {
                var now = DateTime.Now;
                var l30d = now.AddDays(-30);
                var start = new DateTime(l30d.Year, l30d.Month, l30d.Day, 0, 0, 0, 0);
                //var end = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, 999).AddDays(-1);
                var end = DateTime.Now.Date;

                return new DateTimeRange(start, end);
            }
        }

        /// <summary>
        /// 包含今天的过去30天
        /// </summary>
        public static DateTimeRange Last30DaysIncludeToday
        {
            get
            {
                // 包含今天的过去30天 = 之前的29天 + 今天全天（1天)
                var now = DateTime.Now;
                var l30d = now.AddDays(-29);
                var start = new DateTime(l30d.Year, l30d.Month, l30d.Day, 0, 0, 0, 0);
                //var end = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, 999).AddDays(-1);
                var end = DateTime.Now.Date.AddDays(1);

                return new DateTimeRange(start, end);
            }
        }
    }
}
