using System;
using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 场景枚举（支持组合）
/// </summary>
[Flags]
public enum EnumSceneFlags
{
    [Display(Name = "无")]
    None = 0,
    [Display(Name = "创建")]
    Create = 1,
    [Display(Name = "更新")]
    Update = 2,
    [Display(Name = "详情")]
    Details = 4,
    [Display(Name = "全部")]
    All = Create | Update | Details
}