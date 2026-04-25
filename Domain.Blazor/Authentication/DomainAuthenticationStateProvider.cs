using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TKW.Framework.Domain.Interfaces;

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
        DomainHost<TUserInfo> domainHost, IServiceProvider serviceProvider)
    {
        _Logger = domainHost.LoggerFactory.CreateLogger<DomainAuthenticationStateProvider<TUserInfo>>();
        _DomainHost = domainHost ?? throw new ArgumentException();
        _ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ProtectedLocalStorage = protectedLocalStorage ?? throw new ArgumentNullException(nameof(protectedLocalStorage));
    }
    private readonly ILogger<DomainAuthenticationStateProvider<TUserInfo>> _Logger;
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly ProtectedLocalStorage _ProtectedLocalStorage;
    private readonly IServiceProvider _ServiceProvider; // 新增：用于作用域绑定
    private const string SessionKeyStorageName = "TKWF_SessionKey";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // 2. 尝试从浏览器存储读取 SessionKey
            var sessionKeyResult = await _ProtectedLocalStorage.GetAsync<string>(SessionKeyStorageName);
            var sessionKey = sessionKeyResult.Success ? sessionKeyResult.Value : null;

            // 3. 统一使用 Host 的调度逻辑
            // 内部已包含：尝试恢复会话 -> 失败则自动降级为游客
            await using var scope = await _DomainHost.BeginSessionScopeAsync(_ServiceProvider, sessionKey);
            var user = scope.User;

            // 4. 关键：如果 Key 发生了变化（如新创建了游客），同步回存储
            if (sessionKey != user.SessionKey)
            {
                await _ProtectedLocalStorage.SetAsync(SessionKeyStorageName, user.SessionKey);
                _Logger.LogInformation("已为新游客分配 SessionKey: {SessionKey}", user.SessionKey);
            }

            // 5. 转换为 ClaimsPrincipal
            var principal = user.ToClaimsPrincipal();

            _Logger.LogDebug("AuthenticationState 已更新 - 用户: {UserName}, 已认证: {IsAuthenticated}",
                user.UserInfo.UserName, user.IsAuthenticated);

            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            _Logger.LogError(ex, "获取认证状态失败");
            // 降级：返回匿名状态并尝试清理绑定
            DomainUser<TUserInfo>.UnBindScope();
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