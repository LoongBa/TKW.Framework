using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// App 类型
/// </summary>
public enum EnumAppType
{
    [EnumDefault]
    [Display(Name = "未设置")]
    [EnumMember(Value = "未设置")]
    Unset = 0,
    [Display(Name = "微信")]
    [EnumMember(Value = "微信")]
    Wechat = 1,
    [Display(Name = "支付宝")]
    [EnumMember(Value = "支付宝")]
    Alipay = 2,
    [Display(Name = "钉钉")]
    [EnumMember(Value = "钉钉")]
    DingDing = 3,
    [Display(Name = "抖音")]
    [EnumMember(Value = "抖音")]
    Douyin = 4,
    [Display(Name = "TikTok")]
    [EnumMember(Value = "TikTok")]
    Tiktok = 5,
    [Display(Name = "小红书")]
    [EnumMember(Value = "小红书")]
    RedNote = 6,
    [Display(Name = "融易联")]
    [EnumMember(Value = "融易联")]
    ELink = 10,
}

/// <summary>
/// 性别枚举
/// </summary>
public enum GenderEnum
{
    [EnumDefault]
    [Display(Name = "未设置")]
    [EnumMember(Value = "未设置")]
    Unset = 0,
    [Display(Name = "男")]
    [EnumMember(Value = "男")]
    Male = 1,
    [Display(Name = "女")]
    [EnumMember(Value = "女")]
    Female = 2
}