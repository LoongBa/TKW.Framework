using System;
using TKW.Framework.Common.Exceptions;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session
{
    public class SessionException : Exception, ITKWException
    {
        public string Key { get; }
        public SessionExceptionType Type { get; }

        public SessionException(
            string key,
            SessionExceptionType type,
            Exception innerException = null)
            : base($"会话异常：Key='{key}' {type.GetDisplayAttribute()?.Name}", innerException)
        {
            Key = key;
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
}