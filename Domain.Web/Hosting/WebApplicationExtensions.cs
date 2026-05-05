using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public static class WebApplicationExtensions
{
    public static WebAppBuilder<TUserInfo> ConfigWebAppDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder, string? configSection = "DmpOptions",
        Action<DomainWebOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, DomainWebOptions>, new()
    {
        var options = new DomainWebOptions();

        // 1. 自动执行绑定逻辑 (调用 DomainConfigurationBinder)
        if (!string.IsNullOrEmpty(configSection))
            options.Bind(builder, configSection);

        // 2. 执行用户自定义委托（用于覆盖配置或设置无法从配置文件读取的属性）
        configure?.Invoke(options);

        // 3. 强行锁定环境状态
        options.IsDevelopment = builder.Environment.IsDevelopment();

        // 4. 处理 Web 公共逻辑
        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 使用 V4 统一的 AddDomain 扩展
        return builder.Services.AddDomain<TUserInfo, TInitializer, WebAppBuilder<TUserInfo>, DomainWebOptions>(
            builder.Configuration, options,
            (adapter, opt) => new WebAppBuilder<TUserInfo>(adapter, opt)
        );
    }
}