using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TKW.Framework.Common.Exceptions;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Web.Middlewares
{
    /// <summary>
    /// 简单错误处理中间件（转向到指定页面）
    /// </summary>
    public class SimpleErrorHandleMiddleware : AbstractMiddleware
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        /// <param name="exceptionHandler"></param>
        public SimpleErrorHandleMiddleware(RequestDelegate next, ExceptionHandlerDelegate exceptionHandler)
            : base(next)
        {
            ExceptionHandledExceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        }

        public ExceptionHandlerDelegate ExceptionHandledExceptionHandler { get; }

        #region Overrides of AbstractMiddleware

        /// <summary>
        /// 需要进行的操作：注意 use 中间件的顺序
        /// </summary>
        public override Task Invoke(HttpContext context)
        {
            try
            {
                return Next(context);
            }
            catch (Exception e)
            {
                var result = ExceptionHandledExceptionHandler?.Invoke(new ExceptionHandledResultModel(e), e);
                return context.Response.WriteAsync(result.ToJson());
            }
        }

        #endregion
    }
}