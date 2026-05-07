using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Maui.Session;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Hosting;

public class MauiAppBuilder<TUserInfo, TInitializer, TOptions> : DomainAppBuilderBase<MauiAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions, TUserInfo>
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
    where TOptions : DomainOptions, new()
{
    public MauiAppBuilder(IDomainAppBuilderAdapter builder, TOptions options) : base(builder, options)
    {
        Builder.Services.AddSingleton<IMauiInitializeService, MauiDomainBootstrapper<TUserInfo, TOptions>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer, TOptions> NoSession()
    {
        return UseSessionManager<NoSessionManager<TUserInfo>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer, TOptions> UseMauiSession()
    {
        return UseSessionManager<MauiSessionManager<TUserInfo>>();
    }
}