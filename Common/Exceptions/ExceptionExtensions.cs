using System;

namespace TKW.Framework.Common.Exceptions
{
    /// <summary>
    /// 异常的扩展方法
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// 将异常转换为已处理的异常 ExceptionHandled
        /// </summary>
        /// <see cref="ExceptionHandled"/>
        public static ExceptionHandled ToExceptionHandled(this Exception left)
        {
            return new ExceptionHandled(left);
        }
    }
}