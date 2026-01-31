using System;

namespace TKW.Framework.Domain.Exceptions;

/// <summary>
/// 用户权限异常
/// </summary>
public class UserRoleException : Exception
{
    public UserRoleException(UserRoleExceptionType type, string message)
        : base(message)
    {
        Type = type;
    }

    public UserRoleException(UserRoleExceptionType type, string message, Exception innerException)
        : base(message, innerException)
    {
        Type = type;
    }

    /// <summary>
    /// 用户权限验证异常类型
    /// </summary>
    public UserRoleExceptionType Type { get; }
}