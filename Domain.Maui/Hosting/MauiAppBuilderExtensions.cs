using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui.Hosting;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// 为 MAUI 环境配置领域驱动环境
    /// </summary>
    public static MauiAppBuilder<TUserInfo, TInitializer, TOptions> ConfigMauiAppDomain<TUserInfo, TInitializer, TOptions>(
        this Microsoft.Maui.Hosting.MauiAppBuilder builder, // 修正：扩展 MAUI 原生构建器
        Action<TOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TOptions : DomainOptions, new()
    {
        var options = new TOptions();

        // 1. 执行绑定和配置（参考之前讨论的领域自治原则）
        // 假设 MAUI 也有对应的配置节，或者完全靠代码配置
        configure?.Invoke(options);

        // 2. 环境强制锁定
#if DEBUG
        options.IsDevelopment = true;
#else
        options.IsDevelopment = false;
#endif

        // 3. 调用 V4 统一的 AddDomain 扩展
        // 这会自动处理 IOptions 注册、ValidateOnStart 和 DomainHost.Initialize
        return builder.Services.AddDomain<TUserInfo, TInitializer, MauiAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions>(
            builder.Configuration,
            options,
            (adapter, opt) => new MauiAppBuilder<TUserInfo, TInitializer, TOptions>(adapter, opt)
        );
    }
}