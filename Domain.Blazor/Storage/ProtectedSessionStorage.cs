using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace TKWF.Domain.Blazor.Storage;

/// <summary>
/// 封装 ProtectedLocalStorage 的会话存储服务
/// 主要用于持久化 SessionKey，支持 Blazor Server 和 WASM
/// </summary>
public class ProtectedSessionStorage(
    ProtectedLocalStorage protectedStorage,
    ILogger<ProtectedSessionStorage> logger)
{
    private readonly ProtectedLocalStorage _protectedStorage = protectedStorage ?? throw new ArgumentNullException(nameof(protectedStorage));

    private const string SessionKeyStorageName = "TKWF_SessionKey";

    /// <summary>
    /// 读取当前 SessionKey（如果不存在返回 null）
    /// </summary>
    public async Task<string?> GetSessionKeyAsync()
    {
        try
        {
            var result = await _protectedStorage.GetAsync<string>(SessionKeyStorageName);
            if (result.Success)
            {
                logger?.LogDebug("从 ProtectedLocalStorage 读取 SessionKey 成功");
                return result.Value;
            }

            logger?.LogDebug("未找到 SessionKey");
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "读取 SessionKey 失败");
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
            logger?.LogWarning("尝试保存空的 SessionKey，已忽略");
            return;
        }

        try
        {
            await _protectedStorage.SetAsync(SessionKeyStorageName, sessionKey);
            logger?.LogInformation("SessionKey 保存成功");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "保存 SessionKey 失败");
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
            await _protectedStorage.DeleteAsync(SessionKeyStorageName);
            logger?.LogInformation("SessionKey 已清除");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "清除 SessionKey 失败");
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