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

public static class WebApplicationExtensions
{
    /// <summary>
    /// 配置 TKW 领域 Web 环境。
    /// 这是框架的唯一入口，返回 RegisterServicesBuilder 以开启链式配置。
    /// </summary>
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

        // 2. 注入基础设施服务（在 RegisterServices 闭包执行前）
        if (options.AutoAddHttpContextAccessor) 
            builder.Services.AddHttpContextAccessor();

        // 3. 配置领域主机 (Autofac 集成)
        builder.Host
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
                DomainHost<TUserInfo>.Build<TInitializer>(cb, builder.Configuration, options));

        // 4. 准备管道拦截队列
        var pipelineActions = new List<Action<IApplicationBuilder>>();

        // 5. 注入异常处理中间件（确保在所有 Action 的最前端）
        if (options.UseDomainExceptionMiddleware) 
            pipelineActions.Add(app => app.UseMiddleware<DomainExceptionMiddleware>());

        // 6. 注册 IStartupFilter 以将 pipelineActions 注入 ASP.NET Core 执行管道
        builder.Services.AddSingleton<IStartupFilter>(new DomainPipelineFilter(pipelineActions));

        // 7. 注册路由合规性警告服务
        builder.Services.AddHostedService(sp =>
            new RoutingWarningHostedService(sp.GetRequiredService<ILogger<RoutingWarningHostedService>>(), options));

        // 返回构建器，进入 RegisterServices 阶段
        return new RegisterServicesBuilder(builder, options, pipelineActions);
    }
}