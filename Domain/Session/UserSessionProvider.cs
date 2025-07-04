using System;

namespace TKW.Framework.Domain.Session {
    /// <summary>
    /// 用于将 IDomainServer 泛型接口转换为具体类型（DomainUser）
    /// </summary>
    /// <remarks>适用于某些特殊场景如：MvcAuthorizeRequiredAttribute</remarks>
    /// <see cref="TKW.Framework.Web.Mvc.Attributes.MvcAuthorizeRequiredAttribute"/>
    public sealed class UserSessionProvider : IUserSessionProvider
    {
        private readonly IUserHelper _UserHelper;

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public UserSessionProvider(IUserHelper userHelper)
        {
            _UserHelper = userHelper ?? throw new ArgumentNullException(nameof(userHelper));
        }

        public string SessionKey_KeyName => _UserHelper.SessionKey_KeyName;

        public DomainUserSession RetrieveAndActiveUserSession(string sessionKey)
        {
            var session = _UserHelper.RetrieveAndActiveUserSession(sessionKey);
            return session.ToUserSession();
        }

        public DomainUserSession NewGuestSession()
        {
            var session = _UserHelper.NewGuestSession();
            return session.ToUserSession();
        }

        public void GuestOrUserLogout(string sessionKey)
        {
            _UserHelper.GuestOrUserLogout(sessionKey);
        }
    }
}