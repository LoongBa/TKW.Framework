#nullable enable
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Tools;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话管理器，负责处理用户会话的创建、获取、更新和废弃等操作
/// 使用 HybridCache 作为存储后端，支持内存 + 分布式缓存（后期可无缝接入 Redis）
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
    private readonly HybridCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <summary>
    /// 会话过期时间跨度，默认为10分钟
    /// </summary>
    public TimeSpan SessionExpiredTimeSpan { get; } = sessionExpiredTimeSpan ?? TimeSpan.FromMinutes(10);

    /// <summary>
    /// 会话键的名称（目前主要用于日志或调试，实际键名前缀由 sessionKeyPrefix 控制）
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
    public async Task<SessionInfo> NewSessionAsync()
    {
        // 先生成一个新的、唯一的 sessionKey
        var sessionKey = GenerateNewSessionKey();

        // 使用 GetOrCreateAsync 确保：
        // 1. 如果 key 已存在（极罕见情况，如 key 碰撞），返回已有 session
        // 2. 如果不存在，创建新 session 并存入缓存，同时触发创建事件
        return await _cache.GetOrCreateAsync(sessionKey.AsSpan(),
            _ =>
            {
                var session = new SessionInfo(sessionKey, null);
                OnSessionCreated(session);
                return ValueTask.FromResult(session);
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> ContainsSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return result.Exists;
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> GetSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return result.Exists ? result.Value! : throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    /// <inheritdoc/>
    public async Task<SessionInfo> GetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

        return await UpdateAndActiveSessionAsync(sessionKey, s => s).ConfigureAwait(false);
    }

    /// <summary>
    /// 更新会话状态并立即写回缓存（核心封装方法）
    /// 支持 immutable record + 写回模式，确保单机高效、分布式一致
    /// </summary>
    public async Task<SessionInfo> UpdateAndActiveSessionAsync(
        string sessionKey,
        Func<SessionInfo, SessionInfo> updater)
    {
        var current = await GetSessionAsync(sessionKey).ConfigureAwait(false);
        var updated = updater(current).Active();

        await _cache.SetAsync(sessionKey, updated,
            new HybridCacheEntryOptions
            {
                Expiration = SessionExpiredTimeSpan
                // 可选：后期加标签支持批量失效
                // Tags = new[] { "sessions", $"user:{updated.User?.UserIdString}" }
            }).ConfigureAwait(false);

        return updated;
    }

    /// <inheritdoc/>
    public async Task<SessionInfo?> AbandonSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            return null;

        var result = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        if (!result.Exists) return null;

        await _cache.RemoveAsync(sessionKey).ConfigureAwait(false);
        OnSessionAbandon(result.Value!);
        return result.Value;
    }

    /// <summary>
    /// 触发会话创建事件（多委托安全调用）
    /// </summary>
    public virtual void OnSessionCreated(SessionInfo session)
    {
        if (SessionCreated == null) return;

        var handler = SessionCreated;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionCreated)delegateItem)(session.Key, session);
            }
            catch
            {
                // 记录日志，忽略异常，防止影响其他订阅者或主流程
            }
        }
    }

    #endregion

    /// <summary>
    /// 尝试从缓存中获取会话（纯读操作，不创建、不写入、不缓存 null）
    /// </summary>
    private async Task<(bool Exists, SessionInfo? Value)> TryGetSessionInternalAsync(string sessionKey)
    {
        Console.WriteLine("当前 HybridCache 是否使用了自定义上下文？");

        // 尝试获取内部序列化器类型（仅调试用）
        var serializerType = _cache.GetType()
            .GetProperty("Serializer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(_cache)?
            .GetType().FullName;

        Console.WriteLine($"HybridCache 序列化器: {serializerType ?? "未知（反射失败）"}");

        var exists = true;

        var value = await _cache.GetOrCreateAsync<SessionInfo?>(
            sessionKey.AsSpan(),
            _ =>
            {
                exists = false;
                return ValueTask.FromResult<SessionInfo?>(null);
            },
            new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableLocalCacheWrite |
                        HybridCacheEntryFlags.DisableDistributedCacheWrite
            })
            .ConfigureAwait(false);

        return (exists, value);
    }

    /// <summary>
    /// 触发会话废弃事件（多委托安全调用）
    /// </summary>
    protected virtual void OnSessionAbandon(SessionInfo session)
    {
        if (SessionAbandon == null) return;

        var handler = SessionAbandon;
        foreach (var delegateItem in handler.GetInvocationList())
        {
            try
            {
                ((SessionAbandon)delegateItem)(session.Key, session);
            }
            catch
            {
                // 记录日志，忽略异常
            }
        }
    }

    /// <summary>
    /// 生成新的会话键（带前缀）
    /// </summary>
    protected virtual string GenerateNewSessionKey()
    {
        return BatchIdGenerator.GenerateBatchId(64, sessionKeyPrefix);
    }
}
