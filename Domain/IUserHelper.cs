using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain
{
    public interface IUserHelper<T> where T : DomainUser/*, ICopyValues<T>*/
    {
        /// <summary>
        /// 会话 Key 的 KeyName
        /// </summary>
        string SessionKey_KeyName { get; }
        /// <summary>
        /// 获取并激活用户会话
        /// </summary>
        DomainUserSession<T> RetrieveAndActiveUserSession(string sessionKey);
        /// <summary>
        /// 产生新的 Guest 用户会话
        /// </summary>
        /// <returns></returns>
        DomainUserSession<T> NewGuestSession();
        /// <summary>
        /// Guest 或 用户退出登录
        /// </summary>
        /// <param name="sessionKey"></param>
        void GuestOrUserLogout(string sessionKey);

        UserSessionProvider<T> ToUserAuthSessionProvider();

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <exception cref="SessionException">Condition.</exception>
        DomainUserSession<T> UserLogin(string userName, string passWordHashed, UserAuthenticationType authType, string existsSessionKey = null);

    }
}