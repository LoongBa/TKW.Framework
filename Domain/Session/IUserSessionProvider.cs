using System.Threading.Tasks;

namespace TKW.Framework.Domain.Session;

public interface IUserSessionProvider
{
    /// <summary>
    /// 会话 Key 的 KeyName
    /// </summary>
    string SessionKey_KeyName { get; }

    /// <summary>
    /// 产生新的 Guest 用户会话
    /// </summary>
    /// <returns></returns>
    /// <exception cref="SessionException">会话异常</exception>
    Task<SessionInfo> NewGuestSessionAsync();

    /// <summary>
    /// 获取并激活用户会话
    /// </summary>
    /// <exception cref="SessionException">会话异常</exception>
    Task<SessionInfo> RetrieveAndActiveUserSessionAsync(DomainUser user);

    /// <summary>
    /// Guest 或 用户退出登录
    /// </summary>
    /// <param name="user"></param>
    /// <exception cref="SessionException">会话异常</exception>
    Task GuestOrUserLogoutAsync(DomainUser user);
}