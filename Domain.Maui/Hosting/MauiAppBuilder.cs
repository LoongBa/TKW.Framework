using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Maui.Session;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Maui.Hosting;

public class MauiAppBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<MauiAppBuilder<TUserInfo, TInitializer>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    /// <summary>
    /// 明确宣告不使用会话特性，注入 NoSessionManager 实施严格防守。
    /// </summary>
    public MauiAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        UseSessionManagerInternal<TUserInfo, NoSessionManager<TUserInfo>>();
        return this;
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession()
    {
        return UseMauiSession<MauiSessionManager<TUserInfo>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession<TSessionManager>()
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        UseSessionManagerInternal<TUserInfo, TSessionManager>();
        return this;
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession(ISessionManager<TUserInfo> instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        UseSessionManagerInternal(instance);
        return this;
    }
}