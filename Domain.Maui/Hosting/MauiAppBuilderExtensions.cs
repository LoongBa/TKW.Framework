using Autofac.Extensions.DependencyInjection;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

// 引入 Session

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

        builder.ConfigureContainer(new AutofacServiceProviderFactory(), cb =>
        {
            DomainHost<TUserInfo>.Initialize<TInitializer>(cb, builder.Configuration, options);
        });

        return new MauiAppBuilder<TUserInfo, TInitializer>(
            new MauiAppBuilderAdapter(builder), options);
    }
}