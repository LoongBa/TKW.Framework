using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Web.Middlewares;

namespace TKW.Framework.Domain.Web;

public static class WebApplicationExtensions
{
    /// <summary>
    /// 一行配置 TKWF Domain：领域初始化 + Web 适配层中间件注册
    /// </summary>
    public static WebApplicationBuilder ConfigTKWDomain<TUserInfo, TInitializer>(
        this WebApplicationBuilder builder,
        Action<DomainWebConfigurationOptions<TUserInfo>>? configureWeb = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        // 1. 准备 Web 配置选项
        var webOptions = new DomainWebConfigurationOptions<TUserInfo>();
        configureWeb?.Invoke(webOptions);

        // 2. 切换 Autofac 工厂（必须显式）
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(cb =>
            {
                DomainHost<TUserInfo>.Build<TInitializer>(
                    upLevelContainer: cb,
                    configuration: builder.Configuration);
            });

        // 3. 注册 IStartupFilter，在管道构建时自动执行 Web 中间件注册
        builder.Services.AddSingleton<IStartupFilter>(sp => new DomainWebStartupFilter<TUserInfo>(webOptions));

        return builder;
    }

    /// <summary>
    /// IStartupFilter 实现：在 ASP.NET Core 管道构建阶段拿到 IApplicationBuilder
    /// </summary>
    private sealed class DomainWebStartupFilter<TUserInfo>(DomainWebConfigurationOptions<TUserInfo> options)
        : IStartupFilter
        where TUserInfo : class, IUserInfo, new()
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                // 执行所有 Web 层中间件注册
                if (options.UseSessionUserMiddleware)
                {
                    app.UseMiddleware<SessionUserMiddleware<TUserInfo>>();
                }

                if (options.UseDomainExceptionMiddleware)
                {
                    app.UseMiddleware<DomainExceptionMiddleware>();
                }

                // 可以在这里继续添加其他 Web 配置（如 CORS、异常页面等）
                // 但建议把核心中间件放在这里，顺序由注册顺序控制

                // 调用原始管道（继续后续 UseCors、MapGraphQL 等）
                next(app);
            };
        }
    }
}