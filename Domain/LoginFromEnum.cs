using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain;

public enum LoginFromEnum
{
    [Display(Name = "未设置")]
    Unset = -1,
    [Display(Name = "自定义")]
    Customized = 0,
    [Display(Name = "PCWeb")]
    PcWeb = 1,
    [Display(Name = "移动Web")]
    MobileWeb = 2,
    [Display(Name = "手机App")]
    App = 3,
    [Display(Name = "微信公众号")]
    WechatWep = 4,
    [Display(Name = "微信小程序")]
    WechatApp = 5,
    [Display(Name = "支付宝Web")]
    AliPayWeb = 6,
    [Display(Name = "支付宝App")]
    AliPayApp = 7,
    [Display(Name = "ELinkApp")]
    ELinkApp = 8,
    [Display(Name = "测试")]
    Tester = -99,
}