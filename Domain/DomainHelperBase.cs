using System;
using Autofac;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain
{
    /// <summary>
    /// 实现了 IDomainServer 的抽象基类
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public abstract class DomainHelperBase<TUser> : IUserHelper<TUser>
        where TUser : DomainUser, new()
    {
        private SessionManager<TUser> _SessionManager;

        protected SessionManager<TUser> SessionManager //{ get; }
            => _SessionManager ??= (SessionManager<TUser>)DomainHostFactory().Container.Resolve<ISessionCache>();

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        protected DomainHelperBase(Func<DomainHost> hostFactory = null)
        {
            DomainHostFactory = hostFactory?? DomainHost.Factory;
            // MARK: 构造函数被调用时 DomainHost 还未创建实例，
            //SessionManager = DomainHostFactory().Container.Resolve<SessionManager<TUser>>();
        }

        protected Func<DomainHost> DomainHostFactory { get; }

        protected virtual TUser CreateUserInstance()
        {
            var user = new TUser();
            user.SetDomainHostFactory(DomainHostFactory);
            return user;
        }
        protected virtual TUser CreateUserInstance(string userName, UserAuthenticationType authenticationType = UserAuthenticationType.Guest, bool isAuthenticated = false)
        {
            var user = CreateUserInstance();
            user.SetIdentity(userName, authenticationType.ToString(), isAuthenticated);
            return user;
        }

        /// <summary>
        /// 返回新的 Guest 用户：领域层根据需要重载该方法
        /// </summary>
        protected virtual TUser OnGuestUserLogin()
        {
            var random = new Random((int)DateTime.Now.Ticks).Next(10000);
            return CreateUserInstance($"Guest_{random}");
        }

        /// <summary>
        /// 验证用户登录信息并返回用户
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
        public DomainUserSession<TUser> UserLogin(string userName, string passWordHashed, UserAuthenticationType authType, string existsSessionKey = null)
        {
            //调用业务方法：验证用户名、密码
            var user = OnUserLogin(userName.EnsureHasValue(), passWordHashed.EnsureHasValue(), authType);
            if (existsSessionKey.HasValue())
                return SessionManager.ContainsSession(existsSessionKey)
                    ? SessionManager.UpdateSessionValue(existsSessionKey, user).ToUserSession<TUser>()  //更新对应的 User
                    : SessionManager.CreateSession(user).ToUserSession<TUser>();
            return SessionManager.CreateSession(user).ToUserSession<TUser>();
        }

        #region Implementation of IDomainServer<T>

        public string SessionKey_KeyName => SessionManager.SessionKey_KeyName;

        /// <exception cref="SessionException">会话异常</exception>
        public virtual DomainUserSession<TUser> NewGuestSession()
        {
            return SessionManager.CreateSession(OnGuestUserLogin()).ToUserSession<TUser>();
        }

        /// <exception cref="SessionException">会话异常</exception>
        public virtual DomainUserSession<TUser> RetrieveAndActiveUserSession(string sessionKey)
        {
            return SessionManager.GetAndActiveSession(sessionKey).ToUserSession<TUser>();
        }

        /// <exception cref="SessionException">会话异常</exception>
        public virtual bool ContainsSession(string sessionKey)
        {
            return SessionManager.ContainsSession(sessionKey);
        }

        /// <exception cref="SessionException"></exception>
        public virtual void GuestOrUserLogout(string sessionKey)
        {
            SessionManager.AbandonSession(sessionKey);
        }

        #endregion
    }
}