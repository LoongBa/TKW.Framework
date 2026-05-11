using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// 核心配置逻辑：处理配置绑定、环境锁定及 AddDomain 调用
    /// </summary>
    public static TSubBuilder CoreConfigDomain<TUserInfo, TInitializer, TSubBuilder, TOptions>(
        IServiceCollection services, IConfiguration configuration, IHostEnvironment environment,
        string? configSection, Action<TOptions>? configure,
        Func<IDomainAppBuilderAdapter, TOptions, TSubBuilder> builderFactory,
        Action<TOptions, IServiceCollection>? hostSpecificAction = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions, new()
    {
        // 1. 读取配置文件并绑定
        var options = new TOptions();
        var section = configuration.GetSection(configSection.EnsureNotEmptyOrNull(nameof(configSection)));
        if (!section.Exists())
            throw new DomainException($"❌ 核心配置缺失: 找不到名为 '{configSection}' 的配置节，请检查 appsettings.json 或环境变量。");
        section.Bind(options);

        // 2. 执行用户自定义委托
        configure?.Invoke(options);

        // 3. 锁定环境状态
        options.IsDevelopment = environment.IsDevelopment();

        // 4. 执行宿主差异化逻辑 (例如 Web 的 HttpContextAccessor)
        hostSpecificAction?.Invoke(options, services);

        // 5. 调用统一的 AddDomain
        return services.AddDomain<TUserInfo, TInitializer, TSubBuilder, TOptions>(
            configuration, options, builderFactory);
    }

    extension(HostApplicationBuilder builder)
    {
        /// <summary> 为控制台/Worker 宿主配置领域环境 </summary>
        public LocalAppBuilder<TUserInfo, TInitializer, TOptions> ConfigConsoleDomain<TUserInfo, TInitializer, TOptions>(string? configSection = "TKWDomain", Action<TOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            return CoreConfigDomain<TUserInfo, TInitializer, LocalAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions>(
                builder.Services, builder.Configuration, builder.Environment, configSection, configure,
                (adapter, opt) => new LocalAppBuilder<TUserInfo, TInitializer, TOptions>(adapter, opt));
        }

        /// <summary> 为测试环境配置领域环境 (已纠正返回类型错误) </summary>
        public TestAppBuilder<TUserInfo, TInitializer, TOptions> ConfigTestAppDomain<TUserInfo, TInitializer, TOptions>(string? configSection = "TKWDomain", Action<TOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
            where TOptions : DomainOptions, new()
        {
            // 修正点：这里必须传入 TestAppBuilder 的构造工厂
            return CoreConfigDomain<TUserInfo, TInitializer, TestAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions>(
                builder.Services, builder.Configuration, builder.Environment, configSection, configure,
                (adapter, opt) => new TestAppBuilder<TUserInfo, TInitializer, TOptions>(adapter, opt));
        }
    }
}