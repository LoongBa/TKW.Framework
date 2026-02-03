using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain.Exceptions;

/// <summary>
/// 用户权限验证异常类型
/// </summary>
public enum UserRoleExceptionType
{
    /// <summary>
    /// 用户不属于对应的角色
    /// </summary>
    [Display(Name = "用户不属于对应的角色")]
    UserIsNotRole,
    /// <summary>
    /// 其它异常
    /// </summary>
    [Display(Name = "其它异常")]
    OtherException,
}