using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Interfaces;
using TKWF.Domain.Blazor.Authentication;

namespace TKWF.Domain.Blazor.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Blazor 专用的认证状态提供者
    /// </summary>
    public static IServiceCollection AddDomainAuthentication<TUserInfo>(this IServiceCollection services)
        where TUserInfo : class, IUserInfo, new()
    {
        services.AddScoped<AuthenticationStateProvider, DomainAuthenticationStateProvider<TUserInfo>>();
        services.AddScoped<ProtectedLocalStorage>();
        return services;
    }
}