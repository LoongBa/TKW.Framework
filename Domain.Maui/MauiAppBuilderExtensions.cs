using Autofac.Extensions.DependencyInjection;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui;

public static class MauiAppBuilderExtensions
{
    public static DomainGenericHostBuilder<TUserInfo, TInitializer> ConfigTkwDomain<TUserInfo, TInitializer>(
        this MauiAppBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainOptions();
        configure?.Invoke(options);

        // 1. 核心：MAUI 接入 Autofac，并执行领域初始化逻辑
        builder.ConfigureContainer(new AutofacServiceProviderFactory(), cb =>
        {
            // 直接调用领域主机的 Build 方法，这与 Web/Test 层的逻辑完全一致
            DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options);
        });

        // 2. 返回通用构建器
        // 注意：这里需要传入 DomainGenericHostBuilder 所需的底层参数 (见下文的重构建议)
        return new DomainGenericHostBuilder<TUserInfo, TInitializer>(
            new MauiAppBuilderAdapter(builder), // 适配器模式，解决类型不一致问题
            options
        );
    }
}