using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using TKW.Framework.Domain.Testing;
using TKW.Framework.Domain.Testing.Session;

namespace TKW.Framework.Domain.Hosting;

public class TestAppBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<TestAppBuilder<TUserInfo, TInitializer>, DomainOptions, TUserInfo>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    public DomainHost<TUserInfo> Build()
    {
        // 注册测试专用组件
        RegisterServices((services, _) =>
        {
            services.TryAddSingleton<ITestWriter, ConsoleTestWriter>();
            services.Replace(ServiceDescriptor.Singleton<ILoggerFactory, TestOutputLoggerFactory>());
        });

        if (Builder is IHostApplicationBuilderAdapter hostAdapter) hostAdapter.Build();

        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败");
    }

    public TestAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        return UseSessionManager<StatelessSessionManager<TUserInfo>>();
    }

    public TestAppBuilder<TUserInfo, TInitializer> UseTestSession()
    {
        return UseSessionManager<TestSessionManager<TUserInfo>>();
    }
}