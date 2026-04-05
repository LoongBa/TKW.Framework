using Autofac;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Testing.Session;

namespace TKW.Framework.Domain.Testing.Hosting;

public class TestAppBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<TestAppBuilder<TUserInfo, TInitializer>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    public TestAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        UseSessionManagerInternal<TUserInfo, StatelessSessionManager<TUserInfo>>();
        return this;
    }

    public TestAppBuilder<TUserInfo, TInitializer> UseTestSession()
    {
        return UseTestSession<TestSessionManager<TUserInfo>>();
    }

    public TestAppBuilder<TUserInfo, TInitializer> UseTestSession<TSessionManager>()
        where TSessionManager : class, ISessionManager<TUserInfo>
    {
        UseSessionManagerInternal<TUserInfo, TSessionManager>();
        return this;
    }

    public TestAppBuilder<TUserInfo, TInitializer> UseTestSession(ISessionManager<TUserInfo> instance)
    {
        UseSessionManagerInternal(instance);
        return this;
    }

    public DomainHost<TUserInfo> Initialize()
    {
        ConfigureContainer((cb, _) =>
        {
            DomainHost<TUserInfo>.Initialize<TInitializer>(cb, Builder.Configuration, Options);

            // 补充测试必备的日志和容器回退机制
            cb.RegisterType<ConsoleTestWriter>().As<ITestWriter>()
                .IfNotRegistered(typeof(ITestWriter))
                .SingleInstance();

            cb.RegisterType<TestOutputLoggerFactory>().As<Microsoft.Extensions.Logging.ILoggerFactory>()
                .IfNotRegistered(typeof(Microsoft.Extensions.Logging.ILoggerFactory))
                .SingleInstance();
        });

        _ = (Builder as dynamic).Build();

        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败");
    }
}