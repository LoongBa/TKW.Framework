using Autofac;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Testing.Hosting;

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
        builder.ConfigureContainer((cb, _) =>
        {
            // 1. 注入异步上下文桥接器（取代手动 new XunitTestWriter）
            cb.RegisterType<XunitTestOutputBridge>().As<ITestWriter>().SingleInstance();

            // 2. 注册测试日志工厂（会自动解析上面的 Bridge）
            cb.RegisterType<TestOutputLoggerFactory>()
                .As<Microsoft.Extensions.Logging.ILoggerFactory>()
                .SingleInstance();
        });
        return builder;
    }
}