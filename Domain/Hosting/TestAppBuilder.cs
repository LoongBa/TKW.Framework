using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Testing;
using TKW.Framework.Domain.Testing.Session;

namespace TKW.Framework.Domain.Hosting;

public class TestAppBuilder<TUserInfo, TInitializer, TOptions>(
    IDomainAppBuilderAdapter builder, TOptions options)
    : DomainAppBuilderBase<TestAppBuilder<TUserInfo, TInitializer, TOptions>, TOptions, TUserInfo>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo, TOptions>, new()
    where TOptions : DomainOptions, new()
{
    public DomainHost<TUserInfo> Build()
    {
        // 注册测试专用组件
        RegisterServices((services, _) =>
        {
            services.TryAddSingleton<ITestWriter, ConsoleTestWriter>();
            services.Replace(ServiceDescriptor.Singleton<ILoggerFactory, TestOutputLoggerFactory>());
        });
        var sp = Builder.Services.BuildServiceProvider();
        // 必须阻塞等待，否则测试用例开始跑了，数据库还没建好
        sp.UseTKWDomainAsync<TUserInfo, TOptions>().GetAwaiter().GetResult();
        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败");
    }

    public TestAppBuilder<TUserInfo, TInitializer, TOptions> NoSession()
    {
        return UseSessionManager<NoSessionManager<TUserInfo>>();
    }

    public TestAppBuilder<TUserInfo, TInitializer, TOptions> UseTestSession()
    {
        return UseSessionManager<TestSessionManager<TUserInfo>>();
    }
}