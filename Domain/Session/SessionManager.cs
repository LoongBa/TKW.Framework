#nullable enable
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话管理器，负责处理用户会话的创建、获取、更新和废弃等操作
/// 使用 HybridCache 作为存储后端，支持内存 + 分布式缓存（后期可无缝接入 Redis）
/// </summary>
public class SessionManager<TUserInfo>(
    HybridCache cache,
    string sessionKeyPrefix = "session:",
    TimeSpan? sessionExpiredTimeSpan = null,
    string sessionKeyKeyName = "SessionKey") : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly HybridCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public TimeSpan SessionExpiredTimeSpan { get; } = sessionExpiredTimeSpan ?? TimeSpan.FromMinutes(10);

    public string SessionKey_KeyName { get; } = sessionKeyKeyName;

    public event SessionCreated<TUserInfo>? SessionCreated;

    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public async Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        var sessionKey = GenerateNewSessionKey();

        return await _cache.GetOrCreateAsync(sessionKey.AsSpan(),
            _ =>
            {
                var session = new SessionInfo<TUserInfo>(sessionKey, null);
                OnSessionCreated(session);
                return ValueTask.FromResult(session);
            }).ConfigureAwait(false);
    }

    public async Task<bool> ContainsSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return result.Exists;
    }

    public async Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return result.Exists ? result.Value! : throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var current = await GetSessionAsync(sessionKey).ConfigureAwait(false);

        var updated = current.Active();

        await _cache.SetAsync(sessionKey, updated,
            new HybridCacheEntryOptions { Expiration = SessionExpiredTimeSpan })
            .ConfigureAwait(false);

        return updated;
    }

    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
    {
        var current = await GetSessionAsync(sessionKey).ConfigureAwait(false);
        var updated = updater(current);

        await _cache.SetAsync(sessionKey, updated,
                new HybridCacheEntryOptions { Expiration = SessionExpiredTimeSpan })
            .ConfigureAwait(false);

        return updated;
    }

    public async Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            return null;

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        if (!result.Exists) return null;

        await _cache.RemoveAsync(sessionKey).ConfigureAwait(false);
        OnSessionAbandon(result.Value!);
        return result.Value;
    }
    
    public virtual void OnSessionCreated(SessionInfo<TUserInfo> session)
    {
        if (SessionCreated == null) return;

        var handler = SessionCreated;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionCreated<TUserInfo>)delegateItem)(session.Key, session);
            }
            catch
            {
                // 记录日志，忽略异常，防止影响其他订阅者或主流程
            }
        }
    }

    protected virtual void OnSessionAbandon(SessionInfo<TUserInfo> session)
    {
        if (SessionAbandon == null) return;

        var handler = SessionAbandon;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionAbandon<TUserInfo>)delegateItem)(session.Key, session);
            }
            catch
            {
                // 记录日志，忽略异常
            }
        }
    }

    protected virtual string GenerateNewSessionKey()
    {
        return BatchIdGenerator.GenerateBatchId(64, sessionKeyPrefix);
    }

    private async Task<(bool Exists, SessionInfo<TUserInfo>? Value)> TryGetSessionInternalAsync(string sessionKey)
    {
        var exists = true;

        var value = await _cache.GetOrCreateAsync<SessionInfo<TUserInfo>?>(
            sessionKey.AsSpan(),
            _ =>
            {
                exists = false;
                return ValueTask.FromResult<SessionInfo<TUserInfo>?>(null);
            },
            new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableLocalCacheWrite |
                        HybridCacheEntryFlags.DisableDistributedCacheWrite
            })
            .ConfigureAwait(false);

        return (exists, value);
    }
}