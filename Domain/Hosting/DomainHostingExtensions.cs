using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Tools.Mapper;

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
        // 1. 注册并配置具体的派生类
        var optionsBuilder = services.AddOptions<TOptions>().Configure(opt =>
        {
            opt.CopyValuesFrom(options);
        });

        // 2. 转发基类接口
        services.AddSingleton<IOptions<DomainOptions>>(sp => sp.GetRequiredService<IOptions<TOptions>>());
        services.AddSingleton<IOptionsMonitor<DomainOptions>>(sp => sp.GetRequiredService<IOptionsMonitor<TOptions>>());
        services.AddSingleton<IOptionsSnapshot<DomainOptions>>(sp => sp.GetRequiredService<IOptionsSnapshot<TOptions>>());

        // 3. 可选：启动时验证配置
        if (!options.SkipValidation)
        {
            optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
        }

        // 4. 调用 DomainHost.Initialize (静态初始化)
        var host = DomainHost<TUserInfo>.Initialize<TInitializer, TOptions>(services, options, configuration);

        // 5. 🌟 核心修改：注册初始化器和真正的启动任务
        // 先注册为单例，确保 UseTKWDomainAsync 和 HostedService 拿到的是同一个实例
        services.AddSingleton<DomainHostInitializerBase<TUserInfo, TOptions>, TInitializer>();
        services.AddHostedService<DomainInitializerHostedService<TUserInfo, TOptions>>();

        // 6. 创建适配器并返回构建器
        var adapter = new InternalDefaultAdapter(services, configuration);
        return builderFactory(adapter, options);
    }

    /// <summary>
    /// 🌟 核心修改：改为异步方法
    /// 手动触发领域主机的第二阶段绑定
    /// (Web 端由 IHostedService 自动触发，Test/Console 需显式 await 调用)
    /// </summary>
    public static async Task UseTKWDomainAsync<TUserInfo, TOptions>(this IServiceProvider sp)
        where TUserInfo : class, IUserInfo, new()
        where TOptions : DomainOptions, new()
    {
        var initializer = sp.GetService<DomainHostInitializerBase<TUserInfo, TOptions>>();
        if (initializer != null)
        {
            // 在测试或控制台环境中，需要显式 await 保证初始化完成
            await initializer.ServiceProviderBuiltCallbackAsync(sp);
        }
    }

    private class InternalDefaultAdapter(IServiceCollection services, IConfiguration configuration) : IDomainAppBuilderAdapter
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
    }
}

/// <summary>
/// 🌟 核心修改：真正的领域启动服务
/// </summary>
internal class DomainInitializerHostedService<TUserInfo, TOptions>(
    IServiceProvider sp,
    DomainHostInitializerBase<TUserInfo, TOptions> initializer,
    ILogger<DomainInitializerHostedService<TUserInfo, TOptions>> logger) : IHostedService
    where TUserInfo : class, IUserInfo, new()
    where TOptions : DomainOptions, new()
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 执行完整的异步自举逻辑
            await initializer.ServiceProviderBuiltCallbackAsync(sp);
        }
        catch (SystemSetupRequiredException ex)
        {
            // 💡 架构防崩溃机制：
            // 捕获业务引导异常，不能往外抛！否则 Kestrel 服务器不会启动，表现层就无法工作。
            logger.LogWarning(ex, "🚨 领域系统挂起：检测到未完成的业务配置，系统需要进行 Setup 引导。");

            // 将异常存入全局状态（你可以根据需要在 DomainHost 中添加 SetupException 属性）
            // 这样中间件就能读取这个状态并拦截请求
            var root = DomainHost<TUserInfo>.Root;
            if (root != null) root.SetupException = ex;
        }
        catch (InfrastructureInaccessibleException ex)
        {
            // 基础设施崩溃（如数据库连不上），这种属于致命错误，直接抛出让系统崩溃重启是合理的
            logger.LogCritical(ex, "💥 基础设施访问失败，系统启动中止。");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}