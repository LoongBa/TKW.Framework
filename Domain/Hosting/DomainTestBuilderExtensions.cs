using System;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

// 确保引入 AddDomainHost 所在的命名空间

namespace TKW.Framework.Domain.Hosting; // 根据你的项目结构调整命名空间

public static class DomainTestBuilderExtensions
{
    /// <summary>
    /// 为测试环境配置领域驱动环境 (适配 DomainTestFixture)
    /// </summary>
    public static TestAppBuilder<TUserInfo, TInitializer> ConfigTestAppDomain<TUserInfo, TInitializer>(
        this HostApplicationBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        // 核心：复用 HostApplicationBuilderExtensions 中的 AddDomainHost
        // 这将自动处理 DomainHost 的 Initialize 并在内部实例化 Adapter
        return builder.AddDomainHost<TUserInfo, TInitializer, TestAppBuilder<TUserInfo, TInitializer>, DomainOptions>(
            (adapter, opt) =>
            {
                // 执行外部传入的配置回调
                configure?.Invoke(opt);

                // 返回特定的测试构建器
                return new TestAppBuilder<TUserInfo, TInitializer>(adapter, opt);
            });
    }
}