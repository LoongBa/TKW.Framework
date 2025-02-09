using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.DataType
{
    public enum EnumNoneEnabledDisabled
    {
        [Display(Name = "无")]
        None = 0,
        [Display(Name = "可用")]
        Enabled = 1,
        [Display(Name = "禁用")]
        Disabled = -1,
    }
}