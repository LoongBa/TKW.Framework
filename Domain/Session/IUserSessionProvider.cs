using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

public interface IUserSessionProvider<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 会话 Key 的 KeyName
    /// </summary>
    string SessionKeyKeyName { get; }

    /// <summary>
    /// 产生新的 Guest 用户会话
    /// </summary>
    /// <returns></returns>
    /// <exception cref="SessionException">会话异常</exception>
    Task<SessionInfo<TUserInfo>> NewGuestSessionAsync();

    /// <summary>
    /// 获取并激活用户会话
    /// </summary>
    /// <exception cref="SessionException">会话异常</exception>
    Task<SessionInfo<TUserInfo>> RetrieveAndActiveUserSessionAsync(DomainUser<TUserInfo> user);

    /// <summary>
    /// Guest 或 用户退出登录
    /// </summary>
    /// <param name="user"></param>
    /// <exception cref="SessionException">会话异常</exception>
    Task GuestOrUserLogoutAsync(DomainUser<TUserInfo> user);
}