using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using TKW.Framework.Common.Tools;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class DomainHostingExtensions
{
    /// <summary>
    /// V4 统一入口：向应用注入领域层能力
    /// </summary>
    public static TSubBuilder AddDomain<TUserInfo, TInitializer, TSubBuilder, TOptions>(
        this IServiceCollection services, IConfiguration configuration, TOptions options,
        Func<IDomainAppBuilderAdapter, TOptions, TSubBuilder> builderFactory)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions, new()
    {
        // 1. 注册 Options 并启用验证
        var optionsBuilder = services.AddOptions<TOptions>().Configure(opt =>
        {
            opt.CopyValuesFrom(options);
        });
        if (!options.SkipValidation)
        {
            optionsBuilder
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        // 2. 调用 DomainHost.Initialize (静态初始化)
        var host = DomainHost<TUserInfo>.Initialize<TInitializer, TOptions>(services, options, configuration);

        // 3. 注册一个启动任务，用于在 ServiceProvider 构建后同步状态
        services.AddHostedService(sp =>
        {
            var initializer = new TInitializer();
            initializer.ServiceProviderBuiltCallback(sp);
            return new NullHostedService();
        });

        // 4. 创建适配器并返回构建器
        var adapter = new InternalDefaultAdapter(services, configuration);
        return builderFactory(adapter, options);
    }

    /// <summary>
    /// 手动触发领域主机的第二阶段绑定
    /// (Web 端由 IHostedService 自动触发，Test/Local/MAUI需显式或在 Builder 中调用)
    /// </summary>
    public static void UseTKWDomain<TUserInfo, TOptions>(this IServiceProvider sp)
        where TUserInfo : class, IUserInfo, new()
        where TOptions : DomainOptions, new()
    {
        var initializer = sp.GetService<DomainHostInitializerBase<TUserInfo, TOptions>>();
        initializer?.ServiceProviderBuiltCallback(sp);
    }

    private class InternalDefaultAdapter(IServiceCollection services, IConfiguration configuration) : IDomainAppBuilderAdapter
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
    }

    private class NullHostedService : Microsoft.Extensions.Hosting.IHostedService
    {
        public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
    }
}