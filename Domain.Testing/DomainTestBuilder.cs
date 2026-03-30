using Autofac;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Testing;

public class DomainTestBuilder<TUserInfo>(IHostApplicationBuilder builder, DomainOptions options)
    : DomainHostBuilder<DomainTestBuilder<TUserInfo>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
{
    public DomainHost<TUserInfo> Build<TInitializer>()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        // 1. 注册 DomainHost 初始化任务
        ConfigureContainer((cb, _) =>
        {
            DomainHost<TUserInfo>.Build<TInitializer>(cb, Builder.Configuration, Options);
        });

        // 2. 解决无法 Build 的问题：
        // 在新版 .NET 中，IHostApplicationBuilder 的具体实现（如 HostApplicationBuilder）
        // 都有 Build() 方法。如果是通过 Host.CreateApplicationBuilder() 创建的，
        // 我们可以安全地强转为 dynamic 或具体的实现。
        // 更好的办法是：不通过 builder.Build()，而是直接返回 Root。

        // 启动容器构建
        _ = (Builder as dynamic).Build();

        return DomainHost<TUserInfo>.Root ?? throw new InvalidOperationException("DomainHost 初始化失败");
    }
}