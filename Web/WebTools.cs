using System;
using System.Security.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using TKW.Framework.Common.Exceptions;
using TKW.Framework.Web.Users;

namespace TKW.Framework.Web
{
    /// <summary>
    /// 常用的工具方法
    /// </summary>
    public static class WebTools
    {
        #region 工具方法

        /// <summary>
        /// 从 Request 中获取指定 Header 的值
        /// </summary>
        public static string GetValueFromHeaderOrQueryString(string paramName,
            IHeaderDictionary headers,
            IQueryCollection queryString
        )
        {
            if (String.IsNullOrWhiteSpace(paramName))
                throw new ArgumentException("Argument is null or whitespace", nameof(paramName));
            paramName = paramName.Trim();

            //尝试从 Headers 获得
            if (headers != null && headers.ContainsKey(paramName))
                return headers[paramName];

            //尝试从 QueryString 获得
            if (queryString != null && queryString.ContainsKey(paramName))
                return queryString[paramName];

            return null;
        }

        /// <summary>
        /// 从 Request 中获取指定 Header 的值
        /// </summary>
        /// <exception cref="NotImplementedException">总是。</exception>
        public static string GetValueFromSessionOrCookieOrHeaderOrQueryString(
            string paramName, HttpContext context
        )
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (String.IsNullOrWhiteSpace(paramName))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(paramName));

            //尝试从 Session 获取
            var value = context.SafeSession()?.GetString(paramName);
            if (!String.IsNullOrEmpty(value)) return value;

            return GetValueFromHeaderOrQueryString(paramName, context.Request.Headers, context.Request.Query);
        }

        public static void SetValue2SessionOrCookieOrHeaderOrQueryString(HttpContext context, string paramName, string valueString)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (String.IsNullOrWhiteSpace(paramName))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(paramName));
            if (String.IsNullOrWhiteSpace(valueString))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(valueString));

            //尝试写入 Session
            context.SafeSession()?.SetString(paramName, valueString);
            //尝试写入 cookie
            var cookies = context.Response.Cookies;
            try
            {
                cookies.Delete(paramName);
            }
            catch
            {
                // ignored
            }
            try
            {
                cookies.Append(paramName, valueString);
            }
            catch
            {
                // ignored
            }
            //尝试写入 Header
            var headers = context.Response.Headers;
            if (headers.ContainsKey(paramName))
                headers[paramName] = valueString;
            else
                headers.Add(paramName, valueString);
        }

        #endregion

        /// <summary>
        /// 构造自定义错误对象：用于进行 API 错误代码的映射
        /// </summary>
        /// <remarks>是否显示详细错误信息，请调整 GlobalConfiguration.Configuration.IncludeErrorDetailPolicy 设置</remarks>
        public static ExceptionHandledResultModel HandleException(ExceptionContext context, ExceptionHandlerDelegate handler, ExceptionHandledResultModel resultModel)
        {
            var e = context.Exception;

            #region 常见异常处理

            if (e is WebAuthenticationException)
            {
                resultModel.ExceptionHandled.Prompt = "请重新登录。";
                resultModel.IsRedirect2Url = true;
            }
            else if (e is AuthenticationException)
            {

            }

            #endregion

            try
            {
                return handler(resultModel, context.Exception);
            }
            catch (Exception exception)
            {
                throw new ApplicationInitialFailedException(ApplicationInitialFailedType.ProjectExceptionHandler, exception);
            }
        }

/*        public static bool IfViewExistsByViewEngine(ActionContext context, string viewName)
        {
            var viewEngine = context.HttpContext.RequestServices.GetService<IViewEngine>();
            if (viewEngine == null) throw new ArgumentNullException($"无法从上下文中获得 IViewEngine 的实例");
            return viewEngine.FindView(context, viewName, false) != null;
        }*/
    }
}
