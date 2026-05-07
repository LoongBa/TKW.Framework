using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui.Hosting;

public class MauiDomainBootstrapper<TUserInfo, TOptions> : IMauiInitializeService
    where TUserInfo : class, IUserInfo, new()
    where TOptions : DomainOptions, new()
{
    public void Initialize(IServiceProvider services)
    {
        // 自动触发我们写好的扩展方法！
        services.UseTKWDomain<TUserInfo, TOptions>();
    }
}