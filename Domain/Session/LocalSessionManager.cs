using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 跨平台桌面/控制台专属会话管理器：基于单例内存 + DataProtection 加密持久化
/// </summary>
public class LocalSessionManager<TUserInfo> : ISessionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly IDataProtector _Protector;
    private readonly ILogger<LocalSessionManager<TUserInfo>> _Logger;
    private readonly string _SessionFilePath;

    // 单租户环境：全局只有一个当前的 Session
    private SessionInfo<TUserInfo>? _CurrentSession;
    private readonly SemaphoreSlim _Lock = new(1, 1);

    public event SessionCreated<TUserInfo>? SessionCreated;
    public event SessionAbandon<TUserInfo>? SessionAbandon;

    public string SessionKeyKeyName => "LocalSessionKey";

    public LocalSessionManager(DomainOptions options, IDataProtectionProvider protectionProvider,
        ILogger<LocalSessionManager<TUserInfo>> logger)
    {
        _Logger = logger;
        var applicationName = options.ApplicationName;
        // 创建该应用的专属加密器（用途字符串必须唯一）
        _Protector = protectionProvider.CreateProtector("TKW.Domain.Session.v1");

        // 设置会话文件的跨平台保存路径
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName);

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        _SessionFilePath = Path.Combine(appDataFolder, "session.dat");
    }

    /// <summary>
    /// 初始化时尝试从本地加密文件恢复会话（实现无感登录）
    /// 建议在应用的入口点 (如 WPF 的 OnStartup) 主动调用
    /// </summary>
    public async Task InitializeFromStorageAsync()
    {
        if (!File.Exists(_SessionFilePath)) return;

        try
        {
            var encryptedBase64 = await File.ReadAllTextAsync(_SessionFilePath);
            if (string.IsNullOrWhiteSpace(encryptedBase64)) return;

            // 核心解密逻辑
            var json = _Protector.Unprotect(encryptedBase64);
            _CurrentSession = JsonSerializer.Deserialize<SessionInfo<TUserInfo>>(json);

            _Logger.LogInformation("已成功从本地安全存储恢复会话：{Key}", _CurrentSession?.Key);
        }
        catch (Exception ex)
        {
            // 如果密钥失效、文件被篡改或反序列化失败，静默清理残余文件（用户需重新登录）
            _Logger.LogWarning(ex, "从本地安全存储恢复会话失败，文件可能已损坏或密钥已轮换。");
            _CurrentSession = null;
            File.Delete(_SessionFilePath);
        }
    }

    public async Task<SessionInfo<TUserInfo>> NewSessionAsync()
    {
        await _Lock.WaitAsync();
        try
        {
            var sessionKey = $"local_{Guid.NewGuid():N}";
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
                // 桌面端激活通常不需要频繁写盘，为了SSD寿命和性能，仅在更新 UserInfo 时写盘
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
            await SaveToStorageAsync(_CurrentSession); // 业务数据更新，必须持久化加密

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

                if (File.Exists(_SessionFilePath))
                {
                    File.Delete(_SessionFilePath);
                }

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

    /// <summary>
    /// 将会话信息序列化并加密存储到本地文件系统
    /// </summary>
    private async Task SaveToStorageAsync(SessionInfo<TUserInfo> session)
    {
        var json = JsonSerializer.Serialize(session);
        var encryptedBase64 = _Protector.Protect(json); // 核心加密逻辑

        await File.WriteAllTextAsync(_SessionFilePath, encryptedBase64);
    }
}