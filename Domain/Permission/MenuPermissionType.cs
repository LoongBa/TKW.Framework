using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain.Permission;

/// <summary>
/// 菜单权限类型
/// </summary>
public enum MenuPermissionType
{
    [Display(Name = "无权限")]
    None = 0,
    [Display(Name = "显示并可用")]
    Enabled = 1,
    [Display(Name = "显示但禁用")]
    Disabled = 2,
    [Display(Name = "不显示")]
    Invisible = -1,
}