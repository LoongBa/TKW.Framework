using System;

namespace TKW.Framework.Exceptions
{
    public class ApplicationInitialFailedException : TKWException
    {
        #region Implementation of ITKWException

        /// <summary>
        /// 自定义的错误类型（基类）
        /// </summary>
        public override Enum ErrorType => Type;

        public ApplicationInitialFailedType Type { get; }

        public ApplicationInitialFailedException(ApplicationInitialFailedType type, string message = null, Exception innerException = null) : base(message, innerException)
        {
            Type = type;
        }
        public ApplicationInitialFailedException(ApplicationInitialFailedType type, Exception innerException = null) : base($"{type}", innerException)
        {
            Type = type;
        }
        #endregion
    }
}