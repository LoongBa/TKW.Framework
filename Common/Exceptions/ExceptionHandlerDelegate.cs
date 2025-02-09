using System;

namespace TKW.Framework.Common.Exceptions
{
    /// <summary>
    /// 处理异常并返回处理结果
    /// </summary>
    /// <param name="exception">异常</param>
    /// <remarks>注意：此方法不能再抛出任何异常，必须消极容错</remarks>
    /// <returns></returns>
    public delegate ExceptionHandledResultModel ExceptionHandlerDelegate(ExceptionHandledResultModel resultModel, Exception exception);
}