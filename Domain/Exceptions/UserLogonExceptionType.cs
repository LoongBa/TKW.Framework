using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain.Exceptions
{
    /// <summary>
    /// 用户认证异常类型
    /// </summary>
    public enum UserLogonExceptionType
    {
        /// <summary>
        /// 用户账户不存在
        /// </summary>
        [Display(Name = "用户账户不存在", Prompt = "", Description = "")]
        UserAccountNotExists,

        /// <summary>
        /// 错误的用户名或密码
        /// </summary>
        [Display(Name = "错误的用户名或密码")]
        WrongUsernameOrPassword,

        /// <summary>
        /// 账户状态异常
        /// </summary>
        [Display(Name = "账户状态异常")]
        UserAccountStateInvalid,

        /// <summary>
        /// 账户重复
        /// </summary>
        [Display(Name = "账户重复：已存在相同的账户")]
        UserAccountDuplicated,

        [Display(Name = "用户名已存在")]
        UserNameConfict,

        [Display(Name = "用户资料不存在")]
        UserProfileNotExist,
    }
}
/*
    /// <summary>
    /// 用户认证异常类型
    /// </summary>
    public enum AuthenticationExceptionType
    {
        /// <summary>
        /// 用户不存在
        /// </summary>
        UserNotExists,
        /// <summary>
        /// 错误的用户名或密码
        /// </summary>
        WrongUserOrPassword,
        /// <summary>
        /// 用户账户过期了
        /// </summary>
        AccountExpired,
        /// <summary>
        /// 用户账户被冻结
        /// </summary>
        AccountFrozen,
        /// <summary>
        /// 其它异常
        /// </summary>
        OtherException,
    }
*/
