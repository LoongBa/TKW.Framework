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
    public MauiAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        UseSessionManagerInternal<TUserInfo, StatelessSessionManager<TUserInfo>>();
        return this;
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession()
    {
        return UseMauiSession<MauiSessionManager<TUserInfo>>();
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession<TSessionManager>()
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        // 可以在这里插入 MAUI 专属的全局生命周期事件挂载
        UseSessionManagerInternal<TUserInfo, TSessionManager>();
        return this;
    }

    public MauiAppBuilder<TUserInfo, TInitializer> UseMauiSession(ISessionManager<TUserInfo> instance)
    {
        UseSessionManagerInternal(instance);
        return this;
    }

    // [保留的初始化出口] 
    // 注意：MAUI 通常由其自己的 builder.Build() 触发完成，如果需要，可通过扩展方法在 MAUI 管线构建时断言 Root
}