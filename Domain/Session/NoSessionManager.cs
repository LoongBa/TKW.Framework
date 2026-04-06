using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 无会话管理器（纯后台、完全无状态场景）
/// 彻底禁用会话机制。任何与会话相关的读写操作都将触发异常。
/// </summary>
public class NoSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public string SessionKeyKeyName => "None";

    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
        => throw new NotSupportedException("当前配置为无会话模式(NoSession)，不支持创建新会话。");

    public Task<bool> ContainsSessionAsync(string sessionKey) => Task.FromResult(false);

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
        => throw new NotSupportedException("当前配置为无会话模式，不支持获取会话。");

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
        => throw new NotSupportedException("当前配置为无会话模式，不支持激活会话。");

    public Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
        => throw new NotSupportedException("当前配置为无会话模式，不支持更新会话。");

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
        => Task.FromResult<SessionInfo<TUserInfo>?>(null);

    public void OnSessionCreated(SessionInfo<TUserInfo> session) { }
}