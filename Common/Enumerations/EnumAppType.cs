using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// App 类型
/// </summary>
public enum EnumAppType
{
    [Display(Name = "未设置")]
    Unset = 0,
    [Display(Name = "微信")]
    Wechat = 1,
    [Display(Name = "支付宝")]
    Alipay = 2,
    [Display(Name = "钉钉")]
    DingDing = 3,
    [Display(Name = "抖音")]
    Douyin = 4,
    [Display(Name = "TikTok")]
    Tiktok = 5,
    [Display(Name = "融易联")]
    ELink = 2,
}