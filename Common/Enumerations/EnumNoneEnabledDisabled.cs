using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Enumerations;

public enum EnumNoneEnabledDisabled
{
    [Display(Name = "未设置")]
    Unset = 0,
    [Display(Name = "可用")]
    Enabled = 1,
    [Display(Name = "禁用")]
    Disabled = -1,
}