using System;
using TKW.Framework.Common.Exceptions;

namespace TKW.Framework.Domain.Exceptions;

/// <summary>
/// 用户认证异常
/// </summary>
public class UserLogonException : Exception, ITKWException
{
    public string UserNameOrId { get; }
    public UserLogonExceptionType Type { get; }

    public UserLogonException(string userNameOrId, UserLogonExceptionType type, Exception? innerException = null)
        : base($"用户 '{userNameOrId}' 错误类型：{type}", innerException)
    {
        if (string.IsNullOrWhiteSpace(userNameOrId))
            throw new ArgumentException("Argument is null or whitespace", nameof(userNameOrId));

        UserNameOrId = userNameOrId;
        Type = type;
    }

    #region Implementation of ITKWException
    /// <summary>
    /// 自定义的类型（基类）
    /// </summary>
    public Enum ErrorType => Type;
    #endregion

}