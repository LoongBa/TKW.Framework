using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
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
        DomainHost<TUserInfo> domainHost, IServiceProvider serviceProvider, DomainUserAccessor<TUserInfo> userAccessor)
    {
        _DomainHost = domainHost.EnsureNotNull(nameof(domainHost));
        _Logger = domainHost.LoggerFactory.CreateLogger<DomainAuthenticationStateProvider<TUserInfo>>();
        _ServiceProvider = serviceProvider.EnsureNotNull(nameof(serviceProvider));
        _ProtectedLocalStorage = protectedLocalStorage.EnsureNotNull(nameof(protectedLocalStorage));
        _UserAccessor = userAccessor.EnsureNotNull(nameof(userAccessor));
    }
    private readonly ILogger<DomainAuthenticationStateProvider<TUserInfo>> _Logger;
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly ProtectedLocalStorage _ProtectedLocalStorage;
    private readonly IServiceProvider _ServiceProvider; // 新增：用于作用域绑定
    private const string SessionKeyStorageName = "TKWF_SessionKey";
    private readonly DomainUserAccessor<TUserInfo> _UserAccessor;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // 如果缓存里已经有了，直接返回
        if (_UserAccessor.CurrentUser != null)
            return new AuthenticationState(_UserAccessor.CurrentUser.ToClaimsPrincipal());

        // 1. 从存储获取 Key
        var sessionKey = await _ProtectedLocalStorage.GetAsync<string>("Key");

        // 2. 此时环境已经由 CircuitHandler 绑定好了
        // 直接调用 Host 的 internal 恢复逻辑（或 BeginSessionScope）
        var scope = await _DomainHost.BeginSessionScopeAsync(_ServiceProvider, sessionKey.Value);
        var user = scope.User;
        _UserAccessor.CurrentUser = user;

        return new AuthenticationState(user.ToClaimsPrincipal());
    }

    /// <summary>
    /// 登录成功后手动刷新认证状态
    /// </summary>
    public void NotifyUserChanged()
    {
        // 只要身份变了（无论登录还是注销），就触发这个
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// 注销后手动刷新认证状态
    /// </summary>
    public async Task NotifyLogoutAsync()
    {
        await _ProtectedLocalStorage.DeleteAsync(SessionKeyStorageName);
        NotifyUserChanged();
    }
}
public class DomainUserAccessor<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    // 这里存储恢复好的 DomainUser 实例
    public DomainUser<TUserInfo>? CurrentUser { get; set; }
}