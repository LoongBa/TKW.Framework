using System;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// 为测试环境配置领域驱动环境 (适配 DomainTestFixture)
    /// </summary>
    public static TestAppBuilder<TUserInfo, TInitializer, TOptions>  
        ConfigTestAppDomain<TUserInfo, TInitializer, TOptions>(this HostApplicationBuilder builder, string? configSection = "TKWDomain", Action<TOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TOptions : DomainOptions, new()
    {
        var options = new TOptions();

        // 1. 自动执行绑定逻辑 (内部调用你写的 Binder)
        if (!string.IsNullOrEmpty(configSection))
            options.Bind(builder, configSection);

        // 2. 执行用户自定义委托（用于覆盖配置或设置无法从配置文件读取的属性）
        configure?.Invoke(options);

        // 3. 强行锁定环境状态
        options.IsDevelopment = builder.Environment.IsDevelopment();

        // 使用 V4 统一的 AddDomain 扩展
        return builder.Services.AddDomain<TUserInfo, TInitializer, TestAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions>(
            builder.Configuration, options,
            (adapter, opt) => new TestAppBuilder<TUserInfo, TInitializer, TOptions>(adapter, opt)
        );
    }

    extension(HostApplicationBuilder builder)
    {
        /// <summary>
        /// 为本地客户端类宿主配置领域驱动环境 (V4 标准 DI 版)
        /// </summary>
        private LocalAppBuilder<TUserInfo, TInitializer, TOptions> 
            ConfigLocalAppDomain<TUserInfo, TInitializer, TOptions>(
            string? configSection = "TKWDomain", Action<TOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            var options = new TOptions();

            // 1. 自动执行绑定逻辑 (调用 DomainConfigurationBinder)
            if (!string.IsNullOrEmpty(configSection))
                options.Bind(builder, configSection);
            
            // 2. 执行用户自定义委托（用于覆盖配置或设置无法从配置文件读取的属性）
            configure?.Invoke(options);

            // 3. 强行锁定环境状态
            options.IsDevelopment = builder.Environment.IsDevelopment();

            // 使用 V4 统一的 AddDomain 扩展
            return builder.Services.AddDomain<TUserInfo, TInitializer, LocalAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions>(
                    builder.Configuration, options,
                    (adapter, opt) => new LocalAppBuilder<TUserInfo, TInitializer, TOptions>(adapter, opt)
            );
        }

        public LocalAppBuilder<TUserInfo, TInitializer, TOptions> ConfigConsoleDomain<TUserInfo, TInitializer, TOptions>(
            string? configSection = "TKWDomain", Action<TOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            return builder.ConfigLocalAppDomain<TUserInfo, TInitializer, TOptions>(configSection, configure);
        }

        public LocalAppBuilder<TUserInfo, TInitializer, TOptions> ConfigConsoleDomain<TUserInfo, TInitializer, TOptions>(
            Action<TOptions>? configure = null, string? configSection = "TKWDomain")
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            return builder.ConfigLocalAppDomain<TUserInfo, TInitializer, TOptions>(configSection, configure);
        }

        public TestAppBuilder<TUserInfo, TInitializer, TOptions> ConfigTestAppDomainConfigConsoleDomain<TUserInfo, TInitializer, TOptions>(
            Action<TOptions>? configure = null, string? configSection = "TKWDomain")
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            return builder.ConfigTestAppDomain<TUserInfo, TInitializer, TOptions>(configSection, configure);
        }
    }
}