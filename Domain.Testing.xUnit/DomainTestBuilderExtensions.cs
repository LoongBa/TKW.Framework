using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TKW.Framework.Domain.Testing.xUnit;

public static class DomainTestBuilderExtensions
{
    /// <summary>
    /// 封装 xUnit 专用日志桥接逻辑，业务层无需再手动注册 Bridge
    /// </summary>
    public static TestAppBuilder<TUserInfo, TInitializer> UseXunitLogger<TUserInfo, TInitializer>(this TestAppBuilder<TUserInfo, TInitializer> builder)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        // V4 变更：使用 IServiceCollection 的 RegisterServices 钩子替代 Autofac
        builder.RegisterServices((services, _) =>
        {
            // 1. 注入异步上下文桥接器
            services.AddSingleton<ITestWriter, XunitTestOutputBridge>();

            // 2. 注册测试日志工厂（会自动解析上面的 Bridge）
            // 使用 Replace 确保在测试环境中，TestOutputLoggerFactory 具有最高优先级
            services.Replace(ServiceDescriptor.Singleton<Microsoft.Extensions.Logging.ILoggerFactory, TestOutputLoggerFactory>());
        });
        return builder;
    }
}