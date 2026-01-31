using System;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Exceptions;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session;

public class SessionException : Exception, ITKWException
{
    public string SessionKey { get; }
    public SessionExceptionType Type { get; }

    public SessionException(
        string sessionKey,
        SessionExceptionType type,
        Exception innerException = null)
        : base($"会话异常：SessionKey='{sessionKey}' {type.GetDisplayName()}", innerException)
    {
        SessionKey = sessionKey;
        Type = type;
    }

    public SessionException(
        SessionExceptionType type,
        string message = "",
        Exception innerException = null) : base(message, innerException)
    {
        Type = type;
    }

    #region Implementation of ITKWException
    /// <summary>
    /// 自定义的类型（基类）
    /// </summary>
    public Enum ErrorType => Type;
    #endregion
}