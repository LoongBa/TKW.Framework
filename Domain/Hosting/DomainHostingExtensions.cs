using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class DomainHostingExtensions
{
    /// <summary>
    /// V4 统一入口：向应用注入领域层能力
    /// </summary>
    public static TSubBuilder AddDomain<TUserInfo, TInitializer, TSubBuilder, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IDomainAppBuilderAdapter, TOptions, TSubBuilder> builderFactory)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions, new()
    {
        // 1. 初始化 Options
        var options = new TOptions();

        // 2. 调用 DomainHost.Initialize (静态初始化)
        var host = DomainHost<TUserInfo>.Initialize<TInitializer>(services, options, configuration);

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