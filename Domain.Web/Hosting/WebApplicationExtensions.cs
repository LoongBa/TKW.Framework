using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public static class WebApplicationExtensions
{
    /*public static WebAppBuilder<TUserInfo, TOptions> ConfigWebAppDomain<TUserInfo, TInitializer, TOptions>(
        this WebApplicationBuilder builder, string? configSection = "TKWDomain",
        Action<TOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TOptions : DomainWebOptions, new()
    {
        // 1. 读取配置文件并绑定到 TOptions 实例
        var options = new TOptions();
        var section = builder.Configuration.GetSection(configSection.EnsureNotEmptyOrNull(nameof(configSection)));
        section.Bind(options);

        // 2. 执行用户自定义委托（用于覆盖配置或设置无法从配置文件读取的属性）
        configure?.Invoke(options);

        // 3. 强行锁定环境状态
        options.IsDevelopment = builder.Environment.IsDevelopment();

        // 4. 处理 Web 公共逻辑
        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 使用 V4 统一的 AddDomain 扩展
        return builder.Services.AddDomain<TUserInfo, TInitializer, WebAppBuilder<TUserInfo, TOptions>, TOptions>(
            builder.Configuration, options,
            (adapter, opt) => new WebAppBuilder<TUserInfo, TOptions>(adapter, opt)
        );
    }*/
    /// <summary> 为 Web 宿主配置领域环境 </summary>
    public static WebAppBuilder<TUserInfo, TOptions> ConfigWebAppDomain<TUserInfo, TInitializer, TOptions>(
        this WebApplicationBuilder builder, string? configSection = "TKWDomain", Action<TOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TOptions : DomainWebOptions, new()
    {
        return HostApplicationBuilderExtensions.CoreConfigDomain<TUserInfo, TInitializer, WebAppBuilder<TUserInfo, TOptions>, TOptions>
        (
            builder.Services, builder.Configuration, builder.Environment, configSection, configure,
            (adapter, opt) => new WebAppBuilder<TUserInfo, TOptions>(adapter, opt),
            (opt, svc) =>
            {
                // Web 独有的逻辑
                if (opt.AutoAddHttpContextAccessor) svc.AddHttpContextAccessor();
            }
        );
    }
}