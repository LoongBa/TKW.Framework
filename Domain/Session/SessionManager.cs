using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 领域会话管理器实现类
/// 负责处理用户会话的生命周期，支持内存与分布式二级缓存。
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public class SessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly HybridCache _Cache;
    private readonly DomainOptions _Options;

    private IIdGenerator IdGenerator { get; }

    /// <summary>
    /// 初始化会话管理器
    /// </summary>
    /// <param name="cache">HybridCache 缓存实例</param>
    /// <param name="options">领域层全局配置对象</param>
    /// <param name="idGenerator">唯一标识符生成器（可传入性能更好的 NanoId 生成器，默认使用内置生成器）</param>
    public SessionManager(HybridCache cache, DomainOptions options, IIdGenerator? idGenerator = null)
    {
        _Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _Options = options ?? throw new ArgumentNullException(nameof(options));

        IdGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    /// <summary>
    /// 获取会话过期时长（从全局配置获取）
    /// </summary>
    public TimeSpan SessionExpiredTimeSpan => _Options.Session.ExpiredTimeSpan;

    /// <summary>
    /// 获取会话在存储/传输中的键名
    /// </summary>
    public string SessionKeyKeyName => _Options.Session.SessionKeyName;

    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    /// <summary>
    /// 异步创建新会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        var sessionKey = GenerateNewSessionKey();

        return await _Cache.GetOrCreateAsync(sessionKey.AsSpan(),
            _ =>
            {
                var session = new SessionInfo<TUserInfo>(sessionKey, null);
                OnSessionCreated(session);
                return ValueTask.FromResult(session);
            },
            new HybridCacheEntryOptions { Expiration = SessionExpiredTimeSpan })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 异步检查指定会话是否存在
    /// </summary>
    public async Task<bool> ContainsSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return false;
        var (exists, _) = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return exists;
    }

    /// <summary>
    /// 异步获取指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> GetSessionAsync(string sessionKey)
    {
        var (exists, value) = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        return exists ? value! : throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    /// <summary>
    /// 异步获取并激活指定会话（性能优化版）
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync(string sessionKey)
    {
        var user = await TryGetAndActiveSessionAsync(sessionKey).ConfigureAwait(false);
        return user ?? throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
    }

    /// <summary>
    /// 尝试获取并激活指定会话（不抛出 SessionNotFound 异常）
    /// </summary>
    public async Task<SessionInfo<TUserInfo>?> TryGetAndActiveSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return null;

        var (exists, current) = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        if (!exists || current == null) return null;

        // 【滑动过期优化】：半程触发机制
        // 只有当距离上次激活的时间超过了过期时长的一半，才重写缓存。
        var elapsed = DateTime.Now - current.TimeLastActivated;
        if (elapsed > (SessionExpiredTimeSpan / 2))
        {
            var updated = current.Active();
            await _Cache.SetAsync(sessionKey, updated,
                new HybridCacheEntryOptions { Expiration = SessionExpiredTimeSpan })
                .ConfigureAwait(false);
            return updated;
        }

        return current;
    }

    /// <summary>
    /// 异步更新并激活指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync(string sessionKey, Func<SessionInfo<TUserInfo>, SessionInfo<TUserInfo>> updater)
    {
        var current = await GetSessionAsync(sessionKey).ConfigureAwait(false);
        var updated = updater(current).Active();

        await _Cache.SetAsync(sessionKey, updated,
                new HybridCacheEntryOptions { Expiration = SessionExpiredTimeSpan })
            .ConfigureAwait(false);

        return updated;
    }

    /// <summary>
    /// 异步放弃指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>?> AbandonSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return null;

        var (exists, value) = await TryGetSessionInternalAsync(sessionKey).ConfigureAwait(false);
        if (!exists) return null;

        await _Cache.RemoveAsync(sessionKey).ConfigureAwait(false);
        OnSessionAbandon(value!);
        return value;
    }

    #region 内部逻辑与事件触发

    private async Task<(bool Exists, SessionInfo<TUserInfo>? Value)> TryGetSessionInternalAsync(string sessionKey)
    {
        var exists = true;
        var value = await _Cache.GetOrCreateAsync<SessionInfo<TUserInfo>?>(
                sessionKey.AsSpan(),
                _ =>
                {
                    exists = false;
                    return ValueTask.FromResult<SessionInfo<TUserInfo>?>(null);
                },
                new HybridCacheEntryOptions
                {
                    // 仅读取，不写入“空”值到缓存
                    Flags = HybridCacheEntryFlags.DisableLocalCacheWrite |
                            HybridCacheEntryFlags.DisableDistributedCacheWrite
                })
            .ConfigureAwait(false);

        return (exists, value);
    }

    public virtual void OnSessionCreated(SessionInfo<TUserInfo> session)
    {
        SessionCreated?.Invoke(session.Key, session);
    }

    protected virtual void OnSessionAbandon(SessionInfo<TUserInfo> session)
    {
        SessionAbandon?.Invoke(session.Key, session);
    }

    protected virtual string GenerateNewSessionKey()
    {
        // 使用配置的前缀生成 64 位随机 Key
        return IdGenerator.NewId(32, _Options.Session.SessionKeyPrefix);
    }

    #endregion
}