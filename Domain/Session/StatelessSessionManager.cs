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
    // 使用空访问器消除警告
    public event SessionCreated<TUserInfo>? SessionCreated { add { } remove { } }
    public event SessionAbandon<TUserInfo>? SessionAbandon { add { } remove { } }

    public string SessionKeyKeyName => "Authorization";

    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        return Task.FromResult(new SessionInfo<TUserInfo>($"jwt_{Guid.NewGuid():N}", null));
    }

    public Task<bool> ContainsSessionAsync(string sessionKey)
        => Task.FromResult(!string.IsNullOrWhiteSpace(sessionKey));

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

        // 实际应用中应在此处解析 JWT
        var userInfo = new TUserInfo();

        var domainUser = new DomainUser<TUserInfo>
        {
            UserInfo = userInfo
        };
        var sessionInfo = new SessionInfo<TUserInfo>(sessionKey, domainUser);

        return Task.FromResult(sessionInfo);
    }

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
        => GetSessionAsync(sessionKey);

    // 关键修复：使用 async/await 代替 .Result，防止在某些同步上下文（如旧版 ASP.NET）中产生死锁
    public async Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return null;
        try
        {
            return await GetSessionAsync(sessionKey).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
        => throw new InvalidOperationException("无状态令牌模式下无法在服务端更新用户状态，请重新签发 Token。");

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public void OnSessionCreated(SessionInfo<TUserInfo> session) { }
}