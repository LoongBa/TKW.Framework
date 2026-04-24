using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Maui.Hosting;

/// <summary>
/// MAUI 主机构建器适配器
/// </summary>
public class MauiAppBuilderAdapter(MauiAppBuilder builder) : IDomainAppBuilderAdapter
{
    private readonly MauiAppBuilder _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    public IServiceCollection Services => _builder.Services;
    public IConfiguration Configuration => _builder.Configuration;
    public void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null) where TBuilder : notnull
        => _builder.ConfigureContainer(factory, configure);

    public void Build()
    {
        BuildServiceProvider();
    }

    public IServiceProvider BuildServiceProvider()
    {
        var app = _builder.Build();
        return app.Services;
    }
}