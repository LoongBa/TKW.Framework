using System;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public static class HostApplicationBuilderExtensions
{
    // 直接扩展 IHostApplicationBuilder
    public static DomainGenericHostBuilder<TUserInfo, TInitializer> ConfigTkwDomain<TUserInfo, TInitializer>(
        this IHostApplicationBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainOptions();
        configure?.Invoke(options);

        // 【核心】：在框架内部悄悄套上适配器
        var adapter = new HostApplicationBuilderAdapter(builder);

        // 返回统一的领域构建器
        return new DomainGenericHostBuilder<TUserInfo, TInitializer>(adapter, options);
    }
}