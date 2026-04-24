using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Maui.Session;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Hosting;

public class MauiAppBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<MauiAppBuilder<TUserInfo, TInitializer>, DomainOptions, TUserInfo>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    public MauiAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        return UseSessionManager<NoSessionManager<TUserInfo>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession()
    {
        return UseSessionManager<MauiSessionManager<TUserInfo>>();
    }
}