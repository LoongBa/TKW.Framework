using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Maui.Session;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Hosting;

public class MauiAppBuilder<TUserInfo, TInitializer, TOptions>(
    IDomainAppBuilderAdapter builder, TOptions options)
    : DomainAppBuilderBase<MauiAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions, TUserInfo>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
    where TOptions : DomainOptions, new()
{
    public MauiAppBuilder<TUserInfo, TInitializer, TOptions> NoSession()
    {
        return UseSessionManager<NoSessionManager<TUserInfo>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer, TOptions> UseMauiSession()
    {
        return UseSessionManager<MauiSessionManager<TUserInfo>>();
    }
}