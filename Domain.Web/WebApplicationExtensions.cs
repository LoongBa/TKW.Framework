using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;


// ───────────────────────────────────────────────────────────────
// 主入口返回 RegisterServicesBuilder（可选起点）
// ───────────────────────────────────────────────────────────────
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

        builder.Host
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
                DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options));

        var pipelineActions = new List<Action<IApplicationBuilder>>
        {
            // 注入基础中间件（始终执行）
            app =>
            {
                if (options.UseDomainExceptionMiddleware)
                    app.UseMiddleware<DomainExceptionMiddleware>();

                if (options.UseSessionUserMiddleware)
                    app.UseMiddleware<SessionUserMiddleware<TUserInfo>>();
            }
        };

        // 注册统一执行器
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(pipelineActions));

        // 注册警告服务
        builder.Services.AddHostedService(sp =>
            new RoutingWarningHostedService(sp.GetRequiredService<ILogger<RoutingWarningHostedService>>(), options));

        return new RegisterServicesBuilder(builder, options, pipelineActions);
    }
}
