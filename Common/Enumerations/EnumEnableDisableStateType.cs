using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Enumerations;

public enum EnumEnableDisableStateType
{
    [Display(Name = "全部")]
    All = 0,
    [Display(Name = "禁止")]
    Disabled = -1,
    [Display(Name = "允许")]
    Enabled = 1,
}