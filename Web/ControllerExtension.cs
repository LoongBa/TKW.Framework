using System;
using System.Security.Authentication;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TKW.Framework.Web.Results;

namespace TKW.Framework.Web
{
    public static class ControllerExtension
    {
        public static T GetCurrentUser<T>(this Controller controller) where T : class, IPrincipal
        {
            var user = controller.User as T;
            if (user == null)
                throw new AuthenticationException();
            return user;
        }


        /// <summary>
        /// 客户端（浏览器）转向
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="redirectUrl">转向地址</param>
        /// <returns></returns>
        public static ClientRedirectResult ClientRedirect(this Controller controller, string redirectUrl)
        {
            return new ClientRedirectResult(redirectUrl);
        }

        /// <summary>
        /// 设置Cookies
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public static void SetCookies(this Controller controller, string name, string value)
        {
            SetCookies(controller, name, value, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 设置Cookies
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public static void SetCookies(this Controller controller, string name, string value, TimeSpan expires)
        {
            controller.Response.Cookies.Append(name, value, new CookieOptions { Expires = DateTime.Now.Add(expires) });
        }

        /// <summary>
        /// 设置Cookies到指定域
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public static void SetCookiesForDomain(this Controller controller, string name, string value, string domain)
        {
            SetCookiesForDomain(controller, name, value, domain, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 设置Cookies到指定域
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public static void SetCookiesForDomain(this Controller controller, string name, string value, string domain, TimeSpan expires)
        {
            controller.Response.Cookies.Append(name, value, new CookieOptions { Expires = DateTime.Now.Add(expires), Domain = domain });
        }

        /// <summary>
        /// 通过名称读取Cookies对象
        /// </summary>
        public static string Cookies(this Controller controller, string name)
        {
            return controller.Request.Cookies[name];
        }

        /// <summary>
        /// 读取Cookies集合
        /// </summary>
        public static IRequestCookieCollection Cookies(this Controller controller)
        {
            return controller.Request.Cookies;
        }
    }
}