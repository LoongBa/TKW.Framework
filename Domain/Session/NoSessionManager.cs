using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 无会话管理器（纯后台、完全无状态场景）
/// 彻底禁用会话机制。任何与会话相关的读写操作都将触发异常。
/// </summary>
public class NoSessionManager<TUserInfo>(ILogger<NoSessionManager<TUserInfo>> logger) : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    // 使用空访问器消除 CS0067 警告
    public event SessionCreated<TUserInfo>? SessionCreated { add { } remove { } }
    public event SessionAbandon<TUserInfo>? SessionAbandon { add { } remove { } }

    public string SessionKeyKeyName => "None";

    public Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试创建新会话。");
        throw new NotSupportedException("当前配置为无会话模式(NoSession)，不支持创建新会话。");
    }

    public Task<bool> ContainsSessionAsync(string sessionKey)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试检查会话是否存在。");
        return Task.FromResult(false);
    }

    public Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试获取会话。");
        throw new NotSupportedException("当前配置为无会话模式，不支持获取会话。");
    }

    public Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试激活会话。");
        throw new NotSupportedException("当前配置为无会话模式，不支持激活会话。");
    }

    public Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试获取并激活会话。");
        return Task.FromResult<SessionInfo<TUserInfo>?>(null);
    }

    public Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试更新会话。");
        throw new NotSupportedException("当前配置为无会话模式，不支持更新会话。");
    }

    public Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
    {
        logger.LogError("非法操作：在 NoSession 模式下尝试放弃会话。");
        return Task.FromResult<SessionInfo<TUserInfo>?>(null);
    }

    public void OnSessionCreated(SessionInfo<TUserInfo> session) { }
}