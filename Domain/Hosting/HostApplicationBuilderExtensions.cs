using System;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// 为通用宿主配置领域驱动环境 (V4 标准 DI 版)
    /// </summary>
    public static TSubBuilder AddDomainHost<TUserInfo, TInitializer, TSubBuilder, TOptions>(
        this HostApplicationBuilder builder,
        Func<IDomainAppBuilderAdapter, TOptions, TSubBuilder> builderFactory)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions, new()
    {
        var options = new TOptions { IsDevelopment = builder.Environment.IsDevelopment() };
        var adapter = new HostApplicationBuilderAdapter<TUserInfo>(builder);

        // 调用静态初始化
        DomainHost<TUserInfo>.Initialize<TInitializer>(builder.Services, options, builder.Configuration);

        return builderFactory(adapter, options);
    }

    // 快捷方法示例
    public static LocalAppBuilder<TUserInfo, TInitializer> ConfigConsoleAppDomain<TUserInfo, TInitializer>(
        this HostApplicationBuilder builder, Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        return builder.AddDomainHost<TUserInfo, TInitializer, LocalAppBuilder<TUserInfo, TInitializer>, DomainOptions>(
            (adapter, opt) => {
                configure?.Invoke(opt);
                return new LocalAppBuilder<TUserInfo, TInitializer>(adapter, opt);
            });
    }
}