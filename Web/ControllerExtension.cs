using System;
using System.Security.Authentication;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TKW.Framework.Web.Results;

namespace TKW.Framework.Web;

public static class ControllerExtension
{
    /// <param name="controller"></param>
    extension(Controller controller)
    {
        public T GetCurrentUser<T>() where T : class, IPrincipal
        {
            var user = controller.User as T;
            if (user == null)
                throw new AuthenticationException();
            return user;
        }

        /// <summary>
        /// 客户端（浏览器）转向
        /// </summary>
        /// <param name="redirectUrl">转向地址</param>
        /// <returns></returns>
        public ClientRedirectResult ClientRedirect(string redirectUrl)
        {
            return new ClientRedirectResult(redirectUrl);
        }

        /// <summary>
        /// 设置Cookies
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public void SetCookies(string name, string value)
        {
            SetCookies(controller, name, value, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 设置Cookies
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public void SetCookies(string name, string value, TimeSpan expires)
        {
            controller.Response.Cookies.Append(name, value, new CookieOptions { Expires = DateTime.Now.Add(expires) });
        }

        /// <summary>
        /// 设置Cookies到指定域
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public void SetCookiesForDomain(string name, string value, string domain)
        {
            SetCookiesForDomain(controller, name, value, domain, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 设置Cookies到指定域
        /// </summary>
        /// <remarks>存疑：是否需要先删除 Cookie</remarks>
        public void SetCookiesForDomain(string name, string value, string domain, TimeSpan expires)
        {
            controller.Response.Cookies.Append(name, value, new CookieOptions { Expires = DateTime.Now.Add(expires), Domain = domain });
        }

        /// <summary>
        /// 通过名称读取Cookies对象
        /// </summary>
        public string Cookies(string name)
        {
            return controller.Request.Cookies[name];
        }

        /// <summary>
        /// 读取Cookies集合
        /// </summary>
        public IRequestCookieCollection Cookies()
        {
            return controller.Request.Cookies;
        }
    }
}