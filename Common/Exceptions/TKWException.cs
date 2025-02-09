using System;

namespace TKW.Framework.Common.Exceptions
{
    public abstract class TKWException : Exception, ITKWException
    {
        protected TKWException(string message, Exception innerException) : base(message, innerException)
        {
        }
        /// <summary>
        /// 自定义的类型（基类）
        /// </summary>
        public abstract Enum ErrorType { get; }
    }
}