using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 领域应用构建器适配器，解耦底层 Web 框架
/// 统一抽象各平台的应用构建器 (屏蔽 WebApplicationBuilder 和 MauiAppBuilder 的差异)
/// </summary>
public interface IDomainAppBuilderAdapter
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
}