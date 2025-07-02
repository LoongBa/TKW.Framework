using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Enumerations;

public enum EnumNoneReadOnlyReadWrite
{
    [Display(Name = "未设置")]
    Unset = 0,
    [Display(Name = "只读")]
    ReadOnly = 1,
    [Display(Name = "读和写")]
    ReadAndWrite = 2,
}