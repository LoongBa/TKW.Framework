using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

public static class WebApplicationExtensions
{
    public static DomainPipelineBuilder ConfigTkwDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebConfigurationOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainWebConfigurationOptions
        {
            // 1. 表现层主导：自动提取宿主环境的默认配置
            IsDevelopment = builder.Environment.IsDevelopment(),
            ConnectionString = builder.Configuration.GetConnectionString("Default") ?? ""
        };

        // 2. 执行用户自定义配置（允许在 Program.cs 中覆盖默认值）
        configure?.Invoke(options);

        // 3. 将 options 传给领域层进行“守门员”审查和 DI 构建
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
                DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options));

        // 4. 初始化管道构建器
        var pipelineBuilder = new DomainPipelineBuilder(builder, options);

        pipelineBuilder.BeforeRouting(app =>
        {
            if (options.UseDomainExceptionMiddleware) app.UseMiddleware<DomainExceptionMiddleware>();
            if (options.UseSessionUserMiddleware) app.UseMiddleware<SessionUserMiddleware<TUserInfo>>();
        });

        builder.Services.AddHostedService<RoutingWarningHostedService>(sp =>
            new RoutingWarningHostedService(sp.GetRequiredService<ILogger<RoutingWarningHostedService>>(), options));

        return pipelineBuilder;
    }
}