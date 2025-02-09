namespace TKW.Framework.Domain.Exceptions
{
    /// <summary>
    /// 用户权限验证异常类型
    /// </summary>
    public enum UserPrivilegeExceptionType
    {
        /// <summary>
        /// 用户不具备相应的权限
        /// </summary>
        UserHasNoPrivilege,
        /// <summary>
        /// 其它异常
        /// </summary>
        OtherException,
    }

}