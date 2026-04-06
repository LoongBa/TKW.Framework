using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public static class WebApplicationExtensions
{
    public static WebAppBuilder<TUserInfo> ConfigWebAppDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainWebOptions
        {
            IsDevelopment = builder.Environment.IsDevelopment(),
        };
        configure?.Invoke(options);

        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 构建 DomainHost
        builder.Host
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
            {
                DomainHost<TUserInfo>.Initialize<TInitializer>(cb, builder.Configuration, options);
            });

        // 返回构建器，它会在内部自动添加 WebExceptionMiddleware（如果 options 开启）
        return new WebAppBuilder<TUserInfo>(new WebApplicationBuilderAdapter(builder), options);
    }

    public static DomainConfigurationBinder BindOptions(
        this DomainWebOptions cfg, WebApplicationBuilder builder)
    {
        return new DomainConfigurationBinder(builder, cfg);
    }
}