using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public static class WebApplicationExtensions
{
    public static WebAppBuilder<TUserInfo> ConfigWebAppDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainWebOptions { IsDevelopment = builder.Environment.IsDevelopment() };
        configure?.Invoke(options);

        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 使用 V4 统一的 AddDomain 扩展
        return builder.Services.AddDomain<TUserInfo, TInitializer, WebAppBuilder<TUserInfo>, DomainWebOptions>(
            builder.Configuration,
            (adapter, opt) => new WebAppBuilder<TUserInfo>(adapter, opt));
    }

    public static DomainConfigurationBinder BindOptions(this DomainWebOptions cfg, WebApplicationBuilder builder)
    {
        return new DomainConfigurationBinder(builder, cfg);
    }
}