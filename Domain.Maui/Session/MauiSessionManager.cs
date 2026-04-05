using System.Text.Json;

// 引入 MAUI 的安全存储
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Session;

/// <summary>
/// MAUI 专属会话管理器：基于单例内存 + SecureStorage 安全持久化
/// </summary>
public class MauiSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private const string SecureStorageKey = "TKW_Maui_DomainSession";

    // 单租户环境：全局只有一个当前的 Session
    private SessionInfo<TUserInfo>? _CurrentSession;
    private readonly SemaphoreSlim _Lock = new(1, 1);

    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public string SessionKeyKeyName => "MauiSessionKey";

    /// <summary>
    /// 初始化时尝试从安全存储恢复会话（实现无感登录）
    /// </summary>
    public async Task InitializeFromStorageAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(SecureStorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _CurrentSession = JsonSerializer.Deserialize<SessionInfo<TUserInfo>>(json);
            }
        }
        catch
        {
            // 如果反序列化失败或存储被破坏，静默处理（用户需重新登录）
            _CurrentSession = null;
            SecureStorage.Default.Remove(SecureStorageKey);
        }
    }

    public async Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        await _Lock.WaitAsync();
        try
        {
            var sessionKey = $"maui_{Guid.NewGuid():N}";
            _CurrentSession = new SessionInfo<TUserInfo>(sessionKey, null);

            await SaveToStorageAsync(_CurrentSession);
            OnSessionCreated(_CurrentSession);

            return _CurrentSession;
        }
        finally
        {
            _Lock.Release();
        }
    }

    public Task<bool> ContainsSessionAsync(string sessionKey)
    {
        return Task.FromResult(_CurrentSession != null && _CurrentSession.Key == sessionKey);
    }

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        if (_CurrentSession == null || _CurrentSession.Key != sessionKey)
            throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

        return Task.FromResult(_CurrentSession);
    }

    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
    {
        var session = await TryGetAndActiveSessionAsync(sessionKey);
        return session ?? throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    public async Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        if (_CurrentSession != null && _CurrentSession.Key == sessionKey)
        {
            await _Lock.WaitAsync();
            try
            {
                _CurrentSession = _CurrentSession.Active();
                // 每次激活可以选择更新持久化，但为了性能，在 MAUI 中也可以只在数据变更时保存
                // await SaveToStorageAsync(_CurrentSession); 
                return _CurrentSession;
            }
            finally
            {
                _Lock.Release();
            }
        }
        return null;
    }

    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
    {
        await _Lock.WaitAsync();
        try
        {
            if (_CurrentSession == null || _CurrentSession.Key != sessionKey)
                throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

            _CurrentSession = updater(_CurrentSession).Active();
            await SaveToStorageAsync(_CurrentSession); // 业务数据更新，必须持久化

            return _CurrentSession;
        }
        finally
        {
            _Lock.Release();
        }
    }

    public async Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
    {
        await _Lock.WaitAsync();
        try
        {
            if (_CurrentSession != null && _CurrentSession.Key == sessionKey)
            {
                var abandonedSession = _CurrentSession;
                _CurrentSession = null;

                SecureStorage.Default.Remove(SecureStorageKey);
                SessionAbandon?.Invoke(sessionKey, abandonedSession);

                return abandonedSession;
            }
            return null;
        }
        finally
        {
            _Lock.Release();
        }
    }

    public void OnSessionCreated(SessionInfo<TUserInfo> session)
    {
        SessionCreated?.Invoke(session.Key, session);
    }

    private async Task SaveToStorageAsync(SessionInfo<TUserInfo> session)
    {
        var json = JsonSerializer.Serialize(session);
        await SecureStorage.Default.SetAsync(SecureStorageKey, json);
    }
}