using Autofac;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Testing;

namespace TKW.Framework.Domain;

public class DomainGenericHostBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder,
    DomainOptions options)
    : DomainAppBuilderBase<DomainGenericHostBuilder<TUserInfo, TInitializer>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    // DomainTestBuilder.cs (TKW.Framework.Domain.Testing)
    public DomainHost<TUserInfo> Initialize()
    {
        // 1. 注册核心领域主机逻辑
        ConfigureContainer((cb, _) =>
        {
            DomainHost<TUserInfo>.Initialize<TInitializer>(cb, Builder.Configuration, Options);
        });

        // 2. 【保险措施】：自动补全测试日志能力
        ConfigureContainer((cb, _) =>
        {
            // 如果容器中没有任何 ITestWriter 实现（比如忘了调 UseXunitLogger）
            // 则注册一个默认的控制台输出器，防止日志静默消失
            cb.RegisterType<ConsoleTestWriter>()
                .As<ITestWriter>()
                .IfNotRegistered(typeof(ITestWriter))
                .SingleInstance();

            // 确保 ILoggerFactory 使用的是测试工厂而非框架默认的 NullLoggerFactory
            cb.RegisterType<TestOutputLoggerFactory>()
                .As<Microsoft.Extensions.Logging.ILoggerFactory>()
                .IfNotRegistered(typeof(Microsoft.Extensions.Logging.ILoggerFactory))
                .SingleInstance();
        });

        // 3. 执行构建逻辑
        _ = (Builder as dynamic).Build();

        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败");
    }
}