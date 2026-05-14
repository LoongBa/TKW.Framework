using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Enumerations;

public enum EnumTripleState
{
    [Display(Name = "未设置")]
    Unset,
    [Display(Name = "是")]
    True,
    [Display(Name = "否")]
    False,
}