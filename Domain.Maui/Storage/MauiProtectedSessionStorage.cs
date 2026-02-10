using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Storage;

/// <summary>
/// MAUI 平台的会话存储实现（基于 SecureStorage）
/// 与 Blazor 的 ProtectedSessionStorage 接口风格一致
/// </summary>
public class MauiProtectedSessionStorage<TUserInfo>(DomainHost<TUserInfo> domainHost) : ISessionStorage
    where TUserInfo : class, IUserInfo, new()
{
    private readonly ILogger? _Logger = domainHost.LoggerFactory.CreateLogger<MauiProtectedSessionStorage<TUserInfo>>();

    private const string SessionKeyStorageName = "TKWF_SessionKey";

    /// <summary>
    /// 读取当前 SessionKey（如果不存在返回 null）
    /// </summary>
    public async Task<string?> GetSessionKeyAsync()
    {
        try
        {
            var key = await SecureStorage.GetAsync(SessionKeyStorageName);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _Logger?.LogDebug("从 SecureStorage 读取 SessionKey 成功");
                return key;
            }

            _Logger?.LogDebug("未找到 SessionKey");
            return null;
        }
        catch (Exception ex)
        {
            _Logger?.LogWarning(ex, "读取 SecureStorage 失败");
            return null;
        }
    }

    /// <summary>
    /// 保存 SessionKey（覆盖旧值）
    /// </summary>
    public async Task SaveSessionKeyAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            _Logger?.LogWarning("尝试保存空的 SessionKey，已忽略");
            return;
        }

        try
        {
            await SecureStorage.SetAsync(SessionKeyStorageName, sessionKey);
            _Logger?.LogInformation("SessionKey 保存到 SecureStorage 成功");
        }
        catch (Exception ex)
        {
            _Logger?.LogError(ex, "保存 SessionKey 到 SecureStorage 失败");
            throw;
        }
    }

    /// <summary>
    /// 删除当前 SessionKey（注销时调用）
    /// </summary>
    public async Task ClearSessionKeyAsync()
    {
        try
        {
            SecureStorage.Remove("TKWF_SessionKey");
            _Logger?.LogInformation("SessionKey 已从 SecureStorage 删除");
        }
        catch (Exception ex)
        {
            _Logger?.LogWarning(ex, "删除 SecureStorage 中的 SessionKey 失败");
        }
    }

    /// <summary>
    /// 判断是否存在有效的 SessionKey
    /// </summary>
    public async Task<bool> HasSessionKeyAsync()
    {
        var key = await GetSessionKeyAsync();
        return !string.IsNullOrWhiteSpace(key);
    }
}