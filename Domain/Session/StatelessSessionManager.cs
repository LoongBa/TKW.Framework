using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 无状态会话管理器（纯净模式）
/// 专用于纯 JWT 鉴权或完全无状态的 WebAPI 场景。
/// 阻断底层 LocalSessionManager 的自动兜底。
/// </summary>
public class StatelessSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    // 无状态 API 不应该触发这些事件
    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public string SessionKeyKeyName => "Stateless";

    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        // 返回一个临时的匿名会话
        return Task.FromResult(new SessionInfo<TUserInfo>($"stateless_{Guid.NewGuid():N}", null));
    }

    public Task<bool> ContainsSessionAsync(string sessionKey) => Task.FromResult(false);

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
        => throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
        => throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

    public Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
        => throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public void OnSessionCreated(SessionInfo<TUserInfo> session) { }
}