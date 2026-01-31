#nullable enable
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Tools;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话管理器，负责处理用户会话的创建、获取、更新和废弃等操作
/// </summary>
public class SessionManager(
    HybridCache cache,
    string sessionKeyPrefix = "session:",
    TimeSpan? sessionExpiredTimeSpan = null,
    string sessionKeyKeyName = "SessionKey") : ISessionManager
{
    /// <summary>
    /// 混合缓存实例，用于存储会话数据
    /// </summary>
    private readonly HybridCache _Cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <summary>
    /// 会话过期时间跨度，默认为10分钟
    /// </summary>
    public TimeSpan SessionExpiredTimeSpan { get; } = sessionExpiredTimeSpan ?? TimeSpan.FromMinutes(10);

    /// <summary>
    /// 会话键的名称
    /// </summary>
    public string SessionKey_KeyName { get; } = sessionKeyKeyName;

    #region 实现 ISessionManager

    /// <inheritdoc/>
    public event SessionCreated? SessionCreated;

    /// <inheritdoc/>
    public event SessionAbandon? SessionAbandon;

    // 暂不支持分布式可靠触发，建议移除或用 FusionCache
    // public event SessionRemoved? SessionTimeout;

    /// <inheritdoc/>
    public Task<SessionInfo> NewSessionAsync()
    {
        return CreateSessionAsync(null, null);
    }

    /// <inheritdoc/>
    public async Task<bool> ContainsSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        // 使用 GetAsync 进行纯粹的读取操作，避免修改缓存状态
        var session = await _Cache.GetOrCreateAsync<SessionInfo>(sessionKey,
                _ => ValueTask.FromResult<SessionInfo>(null))
            .ConfigureAwait(false);

        return session != null;
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> GetSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var session = await _Cache.GetOrCreateAsync(sessionKey,
                _ => ValueTask.FromResult<SessionInfo>(null))
            .ConfigureAwait(false);

        return session ?? throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> GetOrCreateSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        return await _Cache.GetOrCreateAsync(sessionKey,
            _ =>
            {
                var session = new SessionInfo(sessionKey, null);
                OnSessionCreated(sessionKey, session);
                return ValueTask.FromResult(session);
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> GetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var session = await GetSessionAsync(sessionKey).ConfigureAwait(false);
        session.Active();

        // 直接覆盖设置，更新过期时间，减少 IO 并提高原子性
        await _Cache.SetAsync(sessionKey, session, new HybridCacheEntryOptions
        {
            Expiration = SessionExpiredTimeSpan
        }).ConfigureAwait(false);

        return session;
    }

    /// <inheritdoc/>
    public async Task AbandonSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        // 1. 先尝试获取 Session。如果不存在，GetSessionAsync 会抛出 SessionNotFound 异常。
        var session = await GetSessionAsync(sessionKey).ConfigureAwait(false);

        // 2. 如果存在，则从缓存中移除。注意：RemoveAsync 不返回值。
        await _Cache.RemoveAsync(sessionKey).ConfigureAwait(false);

        // 3. 触发事件
        OnSessionAbandon(sessionKey, session);
    }

    #endregion

    /// <summary>
    /// 创建或获取会话的内部实现
    /// </summary>
    /// <param name="user">用户域对象</param>
    /// <param name="sessionKey">可选的会话键</param>
    /// <returns>创建或获取的会话对象</returns>
    private async Task<SessionInfo> CreateSessionAsync(DomainUser? user, string? sessionKey)
    {
        sessionKey ??= GenerateNewSessionKey();

        var session = await _Cache.GetOrCreateAsync(sessionKey, _ =>
        {
            var newSession = new SessionInfo(sessionKey, user);
            OnSessionCreated(sessionKey, newSession);
            // 可选：在这里记录日志 "Creating new session for user {user.Id}"
            return ValueTask.FromResult(newSession);
        }).ConfigureAwait(false);

        // 可选：检查是否是新创建的（业务需要时）
        // 但 HybridCache 本身无法可靠区分，只能通过其他方式（如加一个 IsNew 标记）

        return session;
    }

    /// <summary>
    /// 触发会话创建事件
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="session">会话对象</param>
    protected virtual void OnSessionCreated(string sessionKey, SessionInfo session)
    {
        if (SessionCreated == null) return;
        var handler = SessionCreated;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionCreated)delegateItem)(sessionKey, session);
            }
            catch
            {
                // 记录日志，忽略异常，防止影响其他订阅者或主流程
            }
        }
    }

    /// <summary>
    /// 触发会话废弃事件
    /// </summary>
    /// <param name="sessionKey">会话键</param>
    /// <param name="session">会话对象</param>
    protected virtual void OnSessionAbandon(string sessionKey, SessionInfo session)
    {
        if (SessionAbandon == null) return;
        var handler = SessionAbandon;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionAbandon)delegateItem)(sessionKey, session);
            }
            catch
            {
                // 记录日志，忽略异常
            }
        }
    }

    // protected virtual void OnSessionTimeout(...) { } // 暂不实现

    /// <summary>
    /// 生成新的缓存键
    /// </summary>
    /// <returns>生成的缓存键</returns>
    protected virtual string GenerateNewSessionKey()
    {
        return BatchIdGenerator.GenerateBatchId(64, sessionKeyPrefix);
    }
}
