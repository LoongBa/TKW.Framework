using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 无状态（令牌式）会话管理器
/// 专门用于 JWT 等无状态鉴权模式。SessionKey 即为 Token 本身。
/// </summary>
public class StatelessSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    // 在 Web API 中，这通常是 Http Header 的名称，比如 Authorization
    public string SessionKeyKeyName => "Authorization";

    // 创建新会话（通常是在登录成功后派发 JWT）
    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        // 返回一个临时的占位符，真正的 Token 通常由授权中心下发
        return Task.FromResult(new SessionInfo<TUserInfo>($"jwt_{Guid.NewGuid():N}", null));
    }

    public Task<bool> ContainsSessionAsync(string sessionKey)
        => Task.FromResult(!string.IsNullOrWhiteSpace(sessionKey));

    // 【核心改造】：通过解析 sessionKey (JWT Token) 还原出用户信息
    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

        // 伪代码：解析 JWT，还原 TUserInfo
        // var userInfo = JwtHelper.ParseToken<TUserInfo>(sessionKey);
        var userInfo = new TUserInfo(); // 示例：你需要根据 Token 解析出具体的 UserInfo

        var domainUser = new DomainUser<TUserInfo>
        {
            UserInfo = userInfo
        };
        var sessionInfo = new SessionInfo<TUserInfo>(sessionKey, domainUser);

        return Task.FromResult(sessionInfo);
    }

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
        => GetSessionAsync(sessionKey);

    public Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return Task.FromResult<SessionInfo<TUserInfo>?>(null);
        return Task.FromResult<SessionInfo<TUserInfo>?>(GetSessionAsync(sessionKey).Result);
    }

    // 状态更新操作在无状态模式下应当被拒绝或忽略
    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
        => throw new InvalidOperationException("无状态令牌模式下无法在服务端更新用户状态，请重新签发 Token。");

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public void OnSessionCreated(SessionInfo<TUserInfo> session) { }
}