using System;
using System.Security.Authentication;

namespace TKW.Framework.Web.Users
{
    public class WebAuthenticationException : AuthenticationException
    {
        public string RequestUrl { get; }

        /// <summary>
        /// 不使用任何消息初始化 <see cref="T:System.Security.Authentication.AuthenticationException"/> 类的新实例。
        /// </summary>
        public WebAuthenticationException(string requestUrl, Exception innerException = null) : base($"requestUrl=[{requestUrl}]", innerException)
        {
            RequestUrl = requestUrl;
        }

        /// <summary>
        /// 用指定消息初始化 <see cref="T:System.Security.Authentication.AuthenticationException"/> 类的新实例。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="message">描述身份验证失败的 <see cref="T:System.String"/>。</param>
        /// <param name="innerException"></param>
        public WebAuthenticationException(string requestUrl, string message, Exception innerException = null) : base(message, innerException)
        {
            RequestUrl = requestUrl;
        }
    }
}