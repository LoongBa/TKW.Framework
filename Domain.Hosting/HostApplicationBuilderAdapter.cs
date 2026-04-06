using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public class HostApplicationBuilderAdapter(HostApplicationBuilder builder) : IDomainAppBuilderAdapter
{
    private readonly HostApplicationBuilder _Builder = builder ?? throw new ArgumentNullException(nameof(builder));

    public IServiceCollection Services => _Builder.Services;

    public IConfiguration Configuration => _Builder.Configuration;

    public void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null)
        where TBuilder : notnull
    {
        // 调用 HostApplicationBuilder 的扩展方法以配置容器工厂（例如 Autofac）
        _Builder.ConfigureContainer(factory, configure);
    }

    public IServiceProvider BuildServiceProvider()
    {
        // Build() 在 HostApplicationBuilder（实际类型）上存在，返回 IHost（其 Services 为最终 IServiceProvider）
        var host = _Builder.Build();
        return host.Services;
    }

    public void Build()
    {
        BuildServiceProvider();
    }
}