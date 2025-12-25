using System;

namespace TKW.Framework.Common.Extensions;

public static class DateTimeUtility
{
    /// <param name="date"></param>
    extension(DateTime date)
    {
        /// <summary>
        /// 获取当前日期实例的0点副本
        /// </summary>
        /// <returns></returns>
        public DateTime GetDayStart()
        {
            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);
        }

        /// <summary>
        /// 获取当前日期实例的24点副本
        /// </summary>
        /// <returns></returns>
        public DateTime GetDayEnd()
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999);
        }

        /// <summary>
        /// 获取当前时间对应的时间戳
        /// </summary>
        /// <returns></returns>
        public ulong TimeStamp()
        {
            var baseDate = new DateTime(1970, 1, 1, 0, 0, 0);
            if (date < baseDate) throw new ArgumentOutOfRangeException(nameof(date), "时间参数不能早于1970年1月1日0时0分0秒");

            return (ulong)(date - baseDate).TotalSeconds;
        }

        /// <summary>
        /// 获取当前时间对应的时间戳
        /// </summary>
        /// <returns></returns>
        // HACK：https://developer.mozilla.org/zh-CN/docs/Web/JavaScript/Reference/Global_Objects/Date
        public ulong UTCTimeStampForJavaScript()
        {
            var baseDate = new DateTime(1970, 1, 1, 0, 0, 0);
            if (date < baseDate) throw new ArgumentOutOfRangeException(nameof(date), "时间参数不能早于1970年1月1日0时0分0秒");

            return (ulong)(date.ToUniversalTime() - baseDate).TotalMilliseconds;
        }
    }
}