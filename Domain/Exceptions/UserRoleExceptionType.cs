namespace TKW.Framework.Domain.Exceptions
{
    /// <summary>
    /// 用户权限验证异常类型
    /// </summary>
    public enum UserRoleExceptionType
    {
        /// <summary>
        /// 用户不属于对应的角色
        /// </summary>
        UserIsNotRole,
        /// <summary>
        /// 其它异常
        /// </summary>
        OtherException,
    }
}