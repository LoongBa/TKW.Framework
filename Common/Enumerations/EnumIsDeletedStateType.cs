using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Enumerations;

public enum EnumIsDeletedState
{
    [Display(Name = "未设置")]
    Unset = 0,
    [Display(Name = "已删除")]
    Deleted = -1,
    [Display(Name = "未删除")]
    UnDeleted = 1,
}