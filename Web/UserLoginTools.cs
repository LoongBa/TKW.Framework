using System;
using Microsoft.AspNetCore.Http;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Web
{
    /// <summary>
    /// 常用的工具方法
    /// </summary>
    public static class UserLoginTools
    {
        #region 用户认证相关的辅助方法

        public static string CurrentSessionKey<T>(HttpContext httpContext, UserSessionProvider<T> userSessionProvider)
            where T : DomainUser, ICopyValues<T>
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            if (userSessionProvider == null)
                throw new ArgumentNullException(nameof(userSessionProvider));

            var sessionKey =
                WebTools.GetValueFromSessionOrCookieOrHeaderOrQueryString(userSessionProvider.SessionKey_KeyName, httpContext);
            return sessionKey;
        }

        public static DomainUserSession<T> UserLoginDone<T>(HttpContext httpContext,
            IUserSessionProvider userSessionProvider, DomainUserSession<T> session, bool createPersistentCookie = false)
            where T : DomainUser, ICopyValues<T>
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            if (userSessionProvider == null)
                throw new ArgumentNullException(nameof(userSessionProvider));
            if (session == null) throw new ArgumentNullException(nameof(session));

            httpContext.User = session.User;
            WebTools.SetValue2SessionOrCookieOrHeaderOrQueryString(httpContext, userSessionProvider.SessionKey_KeyName,
                session.Key);

            return session;
        }

        public static void UserLogoutDone<T>(HttpContext httpContext, UserSessionProvider<T> userSessionProvider)
            where T : DomainUser, ICopyValues<T>
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            var sessionKey = CurrentSessionKey(httpContext, userSessionProvider);
            userSessionProvider.GuestOrUserLogout(sessionKey);
            //清理会话
            httpContext.SafeSession()?.Clear();
            //清理 cookie
            try
            {
                httpContext.Response.Cookies.Delete(userSessionProvider.SessionKey_KeyName);
            }
            catch
            {
                // ignored
            }
        }

        public static DomainUserSession<T> UserLogin<T>(HttpContext context, IUserHelper<T> userHelper, string userName,
            string passWordHashed, UserAuthenticationType desktopWeb, bool createPersistentCookie = false)
            where T : DomainUser, ICopyValues<T>
        {
            //尝试获取 SessionKey
            var sessionKeyKeyName = userHelper.ToUserAuthSessionProvider().SessionKey_KeyName;
            var existsSessionKey = WebTools.GetValueFromSessionOrCookieOrHeaderOrQueryString(sessionKeyKeyName, context);
            //用户通过身份认证
            var userSession = userHelper.UserLogin(userName, passWordHashed, UserAuthenticationType.DesktopWeb,
                existsSessionKey);
            //更新用户认证状态
            return UserLoginDone(context, userHelper.ToUserAuthSessionProvider(), userSession, createPersistentCookie);
        }

        #endregion
    }
}