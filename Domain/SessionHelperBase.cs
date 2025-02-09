using System;
using Autofac;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain
{
    /// <summary>
    /// 实现了 IDomainServer 的抽象基类
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public abstract class SessionHelperBase<TUser> : IUserHelper<TUser>
        where TUser : DomainUser, ICopyValues<TUser>
    {
        private SessionManager<TUser> _SessionManager;

        protected SessionManager<TUser> SessionManager //{ get; }
            => _SessionManager ??= (SessionManager<TUser>)DomainHostFactory().Container.Resolve<ISessionCache>();

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        protected SessionHelperBase(Func<DomainHost> hostFactory)
        {
            hostFactory.AssertNotNull(name: nameof(hostFactory));
            DomainHostFactory = hostFactory;
            // MARK: 构造函数被调用时DomainHost还未创建实例，
            //SessionManager = DomainHostFactory().Container.Resolve<SessionManager<TUser>>();
        }

        protected Func<DomainHost> DomainHostFactory { get; }

        protected abstract TUser CreateUserInstance();

        /// <summary>
        /// 返回新的 Guest 用户
        /// </summary>
        protected abstract TUser OnGuestUserLogin();

        /// <summary>
        /// 用户登录并返回用户
        /// </summary>
        protected abstract TUser OnUserLogin(string userName, string passWordHashed, UserAuthenticationType authType);

        public UserSessionProvider<TUser> ToUserAuthSessionProvider()
        {
            return new UserSessionProvider<TUser>(this);
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <exception cref="SessionException">Condition.</exception>
        public DomainUserSession<TUser> UserLogin(string userName, string passWordHashed, UserAuthenticationType authType = UserAuthenticationType.DesktopWeb, string existsSessionKey = null)
        {
            //验证用户名、密码
            //var domainUser = OnUserLogin(userName, passWordHashed, authType);
            var user = OnUserLogin(userName.EnsureHasValue(), passWordHashed.EnsureHasValue(), authType);
            //if (!string.IsNullOrWhiteSpace(existsSessionKey) && SessionManager.ContainsSession(existsSessionKey))
            return SessionManager.ContainsSession(existsSessionKey.EnsureHasValue())
                ? SessionManager.UpdateSessionValue(existsSessionKey, user).ToUserSession<TUser>()
                : SessionManager.CreateSession(user).ToUserSession<TUser>();
        }

        #region Implementation of IDomainServer<T>

        public string SessionKey_KeyName => SessionManager.SessionKey_KeyName;

        /// <exception cref="SessionException"></exception>
        public virtual DomainUserSession<TUser> NewGuestSession()
        {
            return SessionManager.CreateSession(OnGuestUserLogin()).ToUserSession<TUser>();
        }

        /// <exception cref="SessionException">Condition.</exception>
        public virtual DomainUserSession<TUser> RetrieveAndActiveUserSession(string sessionKey)
        {
            return SessionManager.GetAndActiveSession(sessionKey).ToUserSession<TUser>();
        }

        /// <exception cref="SessionException"></exception>
        public virtual void GuestOrUserLogout(string sessionKey)
        {
            SessionManager.AbandonSession(sessionKey);
        }

        #endregion
    }
}