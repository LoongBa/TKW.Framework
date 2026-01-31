/*using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Web;

/// <summary>
/// 常用的工具方法
/// </summary>
public static class UserLoginTools
{
    #region 用户认证相关的辅助方法

    public static string CurrentSessionKey(HttpContext httpContext, UserSessionProvider userSessionProvider)
    {
        if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
        if (userSessionProvider == null)
            throw new ArgumentNullException(nameof(userSessionProvider));

        var sessionKey =
            WebTools.GetValueFromSessionOrCookieOrHeaderOrQueryString(userSessionProvider.SessionKey_KeyName, httpContext);
        return sessionKey;
    }

    /// <summary>
    /// 完成用户登录操作
    /// </summary>
    /// <param name="httpContext">当前HTTP上下文</param>
    /// <param name="userSessionProvider">用户会话提供者</param>
    /// <param name="session">用户会话对象</param>
    /// <param name="createPersistentCookie">是否创建持久化Cookie</param>
    /// <returns>用户会话对象</returns>
    public static SessionInfo UserLoginDone(HttpContext httpContext,
        IUserHelper userSessionProvider, SessionInfo session, bool createPersistentCookie = false)
    {
        // 检查httpContext是否为null
        if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
        // 检查userSessionProvider是否为null
        if (userSessionProvider == null)
            throw new ArgumentNullException(nameof(userSessionProvider));
        // 检查session是否为null
        if (session == null) throw new ArgumentNullException(nameof(session));

        // 将当前用户设置为httpContext的用户
        httpContext.User = session.User;
        // 将会话键值存储到Session或Cookie或Header或QueryString中
        WebTools.SetValue2SessionOrCookieOrHeaderOrQueryString(httpContext,
            "Session:", session.Key);

        // 返回用户会话对象
        return session;
    }

    /// <summary>
    /// 完成用户登出操作
    /// </summary>
    /// <param name="httpContext">当前HTTP上下文</param>
    /// <param name="userSessionProvider">用户会话提供者</param>
    public static async Task UserLogoutDone(HttpContext httpContext, UserSessionProvider userSessionProvider)
    {
        // 检查httpContext是否为null
        if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
        // 获取当前会话键值
        var sessionKey = CurrentSessionKey(httpContext, userSessionProvider);
        // 执行用户登出操作
        await userSessionProvider.GuestOrUserLogoutAsync(sessionKey);
        // 清理会话
        httpContext.SafeSession()?.Clear();
        // 清理Cookie
        try
        {
            httpContext.Response.Cookies.Delete(userSessionProvider.SessionKey_KeyName);
        }
        catch
        {
            // 忽略异常
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="context">当前HTTP上下文</param>
    /// <param name="userHelper">用户帮助类</param>
    /// <param name="userName">用户名</param>
    /// <param name="passWordHashed">加密后的密码</param>
    /// <param name="authType">认证类型</param>
    /// <param name="createPersistentCookie">是否创建持久化Cookie</param>
    /// <returns>用户会话对象</returns>
    public static SessionInfo UserLogin(HttpContext context, IUserHelper userHelper, string userName,
        string passWordHashed, UserAuthenticationType authType, bool createPersistentCookie = false)
    {
        //TODO: 更新为异步方法，继续检查重构后的调整
        // 尝试获取SessionKey
        var sessionKeyKeyName = userHelper.SessionKey_KeyName;
        var existsSessionKey = WebTools.GetValueFromSessionOrCookieOrHeaderOrQueryString(sessionKeyKeyName, context);
        // 用户通过身份认证
        var userSession = userHelper.UserLoginAsync(null, userName, passWordHashed, authType).Result;
        // 更新用户认证状态
        return UserLoginDone(context, userHelper, userSession, createPersistentCookie);
    }

    #endregion
}*/