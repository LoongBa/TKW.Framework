using System;

namespace TKW.Framework.Domain.Exceptions
{
    /// <summary>
    /// 用户权限异常
    /// </summary>
    public class UserPrivilegeException : Exception
    {
        public UserPrivilegeException(UserPrivilegeExceptionType type, string message)
            : base(message)
        {
            Type = type;
        }

        public UserPrivilegeException(UserPrivilegeExceptionType type, string message, Exception innerException)
            : base(message, innerException)
        {
            Type = type;
        }

        /// <summary>
        /// 用户权限验证异常类型
        /// </summary>
        public UserPrivilegeExceptionType Type { get; }
    }
}