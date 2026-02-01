using System;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TKW.Framework.Web.Users;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Web.Filters;

public class AuthorizeRequiredFilter : IAuthorizationFilter
{
    #region Implementation of IAuthorizationFilter

    /// <summary>
    /// Called early in the filter pipeline to confirm request is authorized.
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext" />.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        #region 尝试恢复会话
        SessionInfo session = null;

        // 尝试获得 SessionKey
        var sessionKey = WebTools.GetValueFromSessionOrCookieOrHeaderOrQueryString(SessionKeyName, context.HttpContext);

        //调用 Delegate 尝试恢复会话
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            try
            {
                session = _ResumeUserSessionDelegate(sessionKey);
            }
            catch (Exception e) //无法创建新会话，抛出异常
            {
                if (_CreateNewUserSessionDelegate == null)
                    throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey, e);
            }
        }

        #endregion

        // 尝试恢复会话无效并且传入了创建新会话的处理，创建新的会话
        session ??= _CreateNewUserSessionDelegate?.Invoke();

        //后续处理
        if (session != null)
        {
            if (_WebUserConvertor != null)
            {
                var webUser = _WebUserConvertor(session);
                webUser.SetContainer(context.HttpContext.Request.Headers["DomainUser-Agent"]); //TODO: Test it! @Happy
                context.HttpContext.User = webUser.ToNewClaimsPrincipal();
            }
            else
            {
                context.HttpContext.User = session.User!.ToNewClaimsPrincipal();
            }

            WebTools.SetValue2SessionOrCookieOrHeaderOrQueryString(context.HttpContext, SessionKeyName, session.Key);
        }

        //跳过 AllowAnonymousAttribute
        var actionDescriptor = (Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor;
        if (actionDescriptor.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), true)
            || actionDescriptor.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), true))
            return;

        if (!AuthorizeCore(context.HttpContext))
            HandleUnauthorizedRequest(context);
    }

    /// <summary>重写时，提供一个入口点用于进行自定义授权检查。</summary>
    /// <returns>如果用户已经过授权，则为 true；否则为 false。</returns>
    /// <param name="httpContext">HTTP 上下文，它封装有关单个 HTTP 请求的所有 HTTP 特定的信息。</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="httpContext" /> 参数为 null。</exception>
    private bool AuthorizeCore(HttpContext httpContext)
    {
        if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
        var user = httpContext.User as IPrincipal;
        return user?.Identity?.IsAuthenticated ?? false;//&& (this._usersSplit.Length <= 0 || ((IEnumerable<string>)this._usersSplit).Contains<string>(domainUser.Identity.Name, (IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase)) && (this._rolesSplit.Length <= 0 || ((IEnumerable<string>)this._rolesSplit).Any<string>(new Func<string, bool>(domainUser.IsInRole)));
    }
    /// <summary>
    /// 重写了基类对于未认证情况的处理方式
    /// </summary>
    /// <param name="context"></param>
    /// <remarks>基类将返回 HandleUnauthorizedRequest 401，改为转向到指定 Controller 和 Action</remarks>
    /// <exception cref="NotImplementedException">总是。</exception>
    /// <exception cref="WebAuthenticationException">Condition.</exception>
    private void HandleUnauthorizedRequest(AuthorizationFilterContext context)
    {
        if (Return401Code) //返回 401
            context.Result = new UnauthorizedResult();
        else
        {
            var url = context.HttpContext.Request.GetDisplayUrl();
            throw new WebAuthenticationException(url, $"未经授权的请求：{url}");
        }
    }

    #endregion
    private readonly Func<string, SessionInfo> _ResumeUserSessionDelegate;

    private readonly Func<SessionInfo> _CreateNewUserSessionDelegate;
    private readonly Func<SessionInfo, WebDomainUser> _WebUserConvertor;
    public bool Return401Code { get; }
    public string SessionKeyName { get; }

    /// <summary>
    /// Mvc会话恢复和认证检查
    /// </summary>
    /// <param name="resumeUserSessionDelegate">恢复会话的委托</param>
    /// <param name="sessionKeyName">约定的SessionKey名称</param>
    /// <param name="return401Code">授权检查失败是否返回401错误（否则抛出异常 WebAuthenticationException）</param>
    public AuthorizeRequiredFilter(Func<string, SessionInfo> resumeUserSessionDelegate, string sessionKeyName = "X-SessionKey", bool return401Code = false)
        : this(resumeUserSessionDelegate, null, sessionKeyName, return401Code)
    {
    }

    /// <summary>
    /// Mvc会话恢复和认证检查
    /// </summary>
    /// <param name="resumeUserSessionDelegate">恢复会话的委托</param>
    /// <param name="createNewUserSessionDelegate">创建新会话的委托</param>
    /// <param name="sessionKeyName">约定的SessionKey名称</param>
    /// <param name="return401Code">授权检查失败是否返回401错误（否则抛出异常 WebAuthenticationException）</param>
    /// <param name="webUserConvertor"></param>
    /// <exception cref="ArgumentNullException"><paramref name="resumeUserSessionDelegate"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException">Argument is null or whitespace</exception>
    public AuthorizeRequiredFilter(Func<string, SessionInfo> resumeUserSessionDelegate,
        Func<SessionInfo> createNewUserSessionDelegate,
        string sessionKeyName = "X-SessionKey",
        bool return401Code = false,
        Func<SessionInfo, WebDomainUser> webUserConvertor = null)
    {
        Return401Code = return401Code;
        if (string.IsNullOrWhiteSpace(sessionKeyName))
            throw new ArgumentException(
                "Argument is null or whitespace",
                nameof(sessionKeyName));
        SessionKeyName = sessionKeyName;
        _ResumeUserSessionDelegate = resumeUserSessionDelegate ?? throw new ArgumentNullException(nameof(resumeUserSessionDelegate));
        _CreateNewUserSessionDelegate = createNewUserSessionDelegate;
        _WebUserConvertor = webUserConvertor;
    }

    /// <summary>
    /// Mvc会话恢复和认证检查
    /// </summary>
    /// <param name="userSessionProvider"></param>
    /// <param name="return401Code"></param>
    /// <param name="webUserConvertor"></param>
    /// <exception cref="ArgumentNullException"><paramref name="userSessionProvider"/> is <see langword="null"/></exception>
    public AuthorizeRequiredFilter(IUserSessionProvider userSessionProvider, bool return401Code = false, Func<SessionInfo, WebDomainUser> webUserConvertor = null)
    {
        /*Return401Code = return401Code;
        if (userSessionProvider == null) throw new ArgumentNullException(nameof(userSessionProvider));
        SessionKeyName = userSessionProvider.SessionKey_KeyName;
        _ResumeUserSessionDelegate = userSessionProvider.RetrieveAndActiveUserSessionAsync;
        _CreateNewUserSessionDelegate = userSessionProvider.NewGuestSessionAsync;
        _WebUserConvertor = webUserConvertor;*/
    }
}