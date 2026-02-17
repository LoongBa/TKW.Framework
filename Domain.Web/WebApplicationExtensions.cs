using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web;

public static class WebApplicationExtensions
{
    public static RegisterServicesBuilder ConfigTkwDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebConfigurationOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainWebConfigurationOptions
        {
            IsDevelopment = builder.Environment.IsDevelopment(),
            ConnectionString = builder.Configuration.GetConnectionString("Default") ?? ""
        };
        configure?.Invoke(options);

        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 构建 DomainHost
        builder.Host
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
            {
                DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options);
            });

        // 返回构建器，它会在内部自动添加 WebExceptionMiddleware（如果 options 开启）
        return new RegisterServicesBuilder(builder, options);
    }
}