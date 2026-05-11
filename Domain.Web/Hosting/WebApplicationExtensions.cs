using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public static class WebApplicationExtensions
{
    /// <summary> 为 Web 宿主配置领域环境 </summary>
    public static WebAppBuilder<TUserInfo, TOptions> ConfigWebAppDomain<TUserInfo, TInitializer, TOptions>(
        this WebApplicationBuilder builder, string? configSection = "TKWDomain", Action<TOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
        where TOptions : DomainWebOptions, new()
    {
        return HostApplicationBuilderExtensions.CoreConfigDomain<TUserInfo, TInitializer, WebAppBuilder<TUserInfo, TOptions>, TOptions>
        (
            builder.Services, builder.Configuration, builder.Environment, configSection, configure,
            (adapter, opt) => new WebAppBuilder<TUserInfo, TOptions>(adapter, opt),
            (opt, svc) =>
            {
                // Web 独有的逻辑
                if (opt.AutoAddHttpContextAccessor) svc.AddHttpContextAccessor();
            }
        );
    }
}