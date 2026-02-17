using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

public static class WebApplicationExtensions
{
    public static RegisterServicesBuilder ConfigTkwDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebConfigurationOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        // 1. 初始化配置
        var options = new DomainWebConfigurationOptions
        {
            IsDevelopment = builder.Environment.IsDevelopment(),
            ConnectionString = builder.Configuration.GetConnectionString("Default") ?? ""
        };
        configure?.Invoke(options);

        // 2. 注入基础设施（RegisterServices 之前）
        if (options.AutoAddHttpContextAccessor)
            builder.Services.AddHttpContextAccessor();

        // 3. 配置领域主机 (Autofac 与宿主日志集成)
        builder.Host
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
            {
                // 注意：Autofac 会通过 Populate 自动获取 builder.Services 里的 ILoggerFactory
                // 此时无需手动注入 builder.LoggingFactory，否则会产生重复的 Provider 导致日志双份输出
                DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options);
            });

        var pipelineActions = new List<Action<IApplicationBuilder>>();

        // 4. 异常中间件 (永远处于管道最顶层)
        if (options.UseDomainExceptionMiddleware)
            pipelineActions.Add(app => app.UseMiddleware<WebExceptionMiddleware>());

        // 5. 挂载管道过滤器
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(pipelineActions));

        return new RegisterServicesBuilder(builder, options, pipelineActions);
    }
}