using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain;

public enum UserAuthenticationType
{
    [Display(Name = "未设置")]
    Unset,
    [Display(Name = "移动Web")]
    MobileWeb,
    [Display(Name = "PCWeb")]
    PCWeb,
    [Display(Name = "微信App")]
    WechatApp,
    [Display(Name = "微信Web")]
    WechatWeb,
    [Display(Name = "支付宝App")]
    AliPayApp,
    [Display(Name = "自定义")]
    Customized,
    [Display(Name = "ELinkApp")]
    ELinkApp,
    [Display(Name = "测试")]
    Tester,
}