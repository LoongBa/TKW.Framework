using Autofac;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Testing;
using TKW.Framework.Domain.Testing.Session;

namespace TKW.Framework.Domain.Hosting;

public class TestAppBuilder<TUserInfo, TInitializer>(
    HostApplicationBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<TestAppBuilder<TUserInfo, TInitializer>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    public DomainHost<TUserInfo> Build()
    {
        ConfigureContainer((cb, _) =>
        {
            cb.RegisterType<ConsoleTestWriter>().As<ITestWriter>()
                .IfNotRegistered(typeof(ITestWriter))
                .SingleInstance();

            cb.RegisterType<TestOutputLoggerFactory>().As<Microsoft.Extensions.Logging.ILoggerFactory>()
                .IfNotRegistered(typeof(Microsoft.Extensions.Logging.ILoggerFactory))
                .SingleInstance();
            DomainHost<TUserInfo>.Initialize<TInitializer>(Options, cb, builder.Configuration);
        });
        builder.Build();
        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败");
    }

    public TestAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        // 调用基类保护方法注册无状态管理器
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

    /// <summary>使用指定的会话管理器（定制完整的会话管理器）</summary>
    /// <typeparam name="TUserInfo"></typeparam>
    /// <typeparam name="TSessionManager"></typeparam>
    public TestAppBuilder<TUserInfo, TInitializer> UseSessionManager<TSessionManager>()
        where TSessionManager : ISessionManager<TUserInfo>
    {
        UseSessionManagerInternal<TUserInfo, TSessionManager>();
        return this;
    }

    /// <summary>使用指定的会话管理器（定制完整的会话管理器）</summary>
    public TestAppBuilder<TUserInfo, TInitializer> UseSessionManager(ISessionManager<TUserInfo> instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        UseSessionManagerInternal(instance);
        return this;
    }
}