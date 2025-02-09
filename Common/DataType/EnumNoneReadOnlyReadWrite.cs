using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.DataType
{
    public enum EnumNoneReadOnlyReadWrite
    {
        [Display(Name = "Ã»ÓÐÈ¨ÏÞ")]
        None = 0,
        [Display(Name = "Ö»¶Á")]
        ReadOnly = 1,
        [Display(Name = "¶ÁÐ´")]
        ReadAndWrite = 2,
    }
}