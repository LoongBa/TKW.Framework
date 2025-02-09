namespace TKW.Framework.Domain.Session
{
    public interface IUserSessionProvider
    {
        /// <summary>
        /// 会话 Key 的 KeyName
        /// </summary>
        string SessionKey_KeyName { get; }
        /// <summary>
        /// 获取并激活用户会话
        /// </summary>
        DomainUserSession<DomainUser> RetrieveAndActiveUserSession(string sessionKey);
        /// <summary>
        /// 产生新的 Guest 用户会话
        /// </summary>
        /// <returns></returns>
        DomainUserSession<DomainUser> NewGuestSession();
        /// <summary>
        /// Guest 或 用户退出登录
        /// </summary>
        /// <param name="sessionKey"></param>
        void GuestOrUserLogout(string sessionKey);
    }
}