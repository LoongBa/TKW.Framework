using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Testing.Session;

/// <summary>
/// 单元测试专用会话管理器：基于 AsyncLocal 实现，保证并发测试用例间的会话绝对隔离
/// </summary>
public class TestSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    // 核心：利用 AsyncLocal 保证每个测试线程（包含其子线程）拥有独立的 Session
    private readonly AsyncLocal<SessionInfo<TUserInfo>?> _CurrentSession = new();

    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public string SessionKeyKeyName => "TestSessionKey";

    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        // 测试环境固定或生成简单的 GUID 即可
        var sessionKey = $"test_session_{Guid.NewGuid():N}";
        var session = new SessionInfo<TUserInfo>(sessionKey, null);

        _CurrentSession.Value = session;
        OnSessionCreated(session);

        return Task.FromResult(session);
    }

    public Task<bool> ContainsSessionAsync(string sessionKey)
    {
        return Task.FromResult(_CurrentSession.Value != null && _CurrentSession.Value.Key == sessionKey);
    }

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        if (_CurrentSession.Value == null || _CurrentSession.Value.Key != sessionKey)
            throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

        return Task.FromResult(_CurrentSession.Value);
    }

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
    {
        var session = GetSessionAsync(sessionKey).GetAwaiter().GetResult();
        _CurrentSession.Value = session.Active();
        return Task.FromResult(_CurrentSession.Value);
    }

    public Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        if (_CurrentSession.Value != null && _CurrentSession.Value.Key == sessionKey)
        {
            _CurrentSession.Value = _CurrentSession.Value.Active();
            return Task.FromResult<SessionInfo<TUserInfo>?>(_CurrentSession.Value);
        }
        return Task.FromResult<SessionInfo<TUserInfo>?>(null);
    }

    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
    {
        var session = GetSessionAsync(sessionKey).GetAwaiter().GetResult();
        _CurrentSession.Value = updater(session).Active();
        return Task.FromResult(_CurrentSession.Value);
    }

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
    {
        var session = _CurrentSession.Value;
        if (session != null && session.Key == sessionKey)
        {
            _CurrentSession.Value = null;
            SessionAbandon?.Invoke(sessionKey, session);
            return Task.FromResult<SessionInfo<TUserInfo>?>(session);
        }
        return Task.FromResult<SessionInfo<TUserInfo>?>(null);
    }

    public void OnSessionCreated(SessionInfo<TUserInfo> session)
    {
        SessionCreated?.Invoke(session.Key, session);
    }
}