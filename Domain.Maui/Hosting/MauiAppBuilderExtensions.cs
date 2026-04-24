using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui.Hosting;

public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder<TUserInfo, TInitializer> ConfigMauiAppDomain<TUserInfo, TInitializer>(
        this MauiAppBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainOptions();
        configure?.Invoke(options);

        // V4 变更：直接使用 builder.Services (IServiceCollection) 进行初始化，不再需要 Autofac 适配
        DomainHost<TUserInfo>.Initialize<TInitializer>(builder.Services, options, builder.Configuration);

        return new MauiAppBuilder<TUserInfo, TInitializer>(
            new MauiAppBuilderAdapter(builder), options);
    }
}