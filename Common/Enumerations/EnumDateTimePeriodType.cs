using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 周期类型
/// </summary>
public enum EnumDateTimePeriodType
{
    /// <summary>
    /// 未设置
    /// </summary>
    [Display(Name = "未设置")]
    Unset = 0,
    /// <summary>
    /// 年
    /// </summary>
    [Display(Name = "年")]
    Year = 1,
    /// <summary>
    /// 季度
    /// </summary>
    [Display(Name = "季度")]
    Quarter = 2,
    /// <summary>
    /// 月
    /// </summary>
    [Display(Name = "月")]
    Month = 3,
    /// <summary>
    /// 星期
    /// </summary>
    [Display(Name = "星期")]
    Week = 4,
    /// <summary>
    /// 日
    /// </summary>
    [Display(Name = "日")]
    Day = 5,
    /// <summary>
    /// 小时
    /// </summary>
    [Display(Name = "小时")]
    Hour = 6,
}