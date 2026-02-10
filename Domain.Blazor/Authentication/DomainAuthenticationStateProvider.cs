using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Blazor.Authentication;

/// <summary>
/// Blazor 专用的 AuthenticationStateProvider
/// 从 DomainHost 获取当前 DomainUser 并转换为 AuthenticationState
/// </summary>
/// <remarks>
/// 1. 支持 Blazor Server 和 WASM（通过 ProtectedLocalStorage 持久化 SessionKey）
/// 2. 登录/注销时自动通知 UI 刷新
/// 3. 与 TKWF.Domain 深度集成，无需额外 HttpContext
/// </remarks>
public class DomainAuthenticationStateProvider<TUserInfo> : AuthenticationStateProvider
    where TUserInfo : class, IUserInfo, new()
{
    private readonly ISessionManager<TUserInfo> _SessionManager;
    private readonly ILogger<DomainAuthenticationStateProvider<TUserInfo>> _Logger;
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly ProtectedLocalStorage _ProtectedLocalStorage;

    /// <summary>
    /// Blazor 专用的 AuthenticationStateProvider
    /// 从 DomainHost 获取当前 DomainUser 并转换为 AuthenticationState
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <remarks>
    /// 1. 支持 Blazor Server 和 WASM（通过 ProtectedLocalStorage 持久化 SessionKey）
    /// 2. 登录/注销时自动通知 UI 刷新
    /// 3. 与 TKWF.Domain 深度集成，无需额外 HttpContext
    /// </remarks>
    public DomainAuthenticationStateProvider(ProtectedLocalStorage protectedLocalStorage,
        DomainHost<TUserInfo> domainHost, ISessionManager<TUserInfo> sessionManager)
    {
        _Logger = domainHost.LoggerFactory.CreateLogger<DomainAuthenticationStateProvider<TUserInfo>>();
        _DomainHost = domainHost ?? throw new ArgumentException();

        _SessionManager = sessionManager;
        _ProtectedLocalStorage = protectedLocalStorage ?? throw new ArgumentNullException(nameof(protectedLocalStorage));
    }

    private const string SessionKeyStorageName = "TKWF_SessionKey";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // 1. 尝试从 ProtectedLocalStorage 读取 SessionKey
            var sessionKeyResult = await _ProtectedLocalStorage.GetAsync<string>(SessionKeyStorageName);
            string? sessionKey = sessionKeyResult.Success ? sessionKeyResult.Value : null;

            DomainUser<TUserInfo>? domainUser = null;

            // 2. 如果有 SessionKey，尝试加载用户
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                var session = await _SessionManager.GetAndActiveSessionAsync(sessionKey);
                domainUser = session.User;
            }

            // 3. 无有效用户 → 创建游客（可选，根据业务决定是否自动创建）
            if (domainUser == null)
            {
                var guestSession = await _DomainHost.NewGuestSessionAsync();
                domainUser = guestSession.User!;

                // 保存游客 SessionKey（保持会话连续性）
                await _ProtectedLocalStorage.SetAsync(SessionKeyStorageName, guestSession.Key);
            }

            // 4. 转换为 ClaimsPrincipal
            var principal = domainUser.ToClaimsPrincipal();

            _Logger.LogDebug("AuthenticationState 已更新 - 用户: {UserName}, 已认证: {IsAuthenticated}",
                domainUser.UserInfo.UserName, domainUser.IsAuthenticated);

            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "获取认证状态失败");
            // 降级：返回匿名状态
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// 登录成功后手动刷新认证状态
    /// </summary>
    public async Task NotifyLoginAsync()
    {
        // 可选：重新从 storage 读取最新 SessionKey
        var sessionKeyResult = await _ProtectedLocalStorage.GetAsync<string>(SessionKeyStorageName);
        if (sessionKeyResult.Success)
        {
            // 强制刷新
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    /// <summary>
    /// 注销后手动刷新认证状态
    /// </summary>
    public async Task NotifyLogoutAsync()
    {
        await _ProtectedLocalStorage.DeleteAsync(SessionKeyStorageName);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}