using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui.Hosting;

/// <summary>
/// MAUI 主机构建器适配器
/// </summary>
public class MauiAppBuilderAdapter(MauiAppBuilder builder) : IDomainAppBuilderAdapter
{
    private readonly MauiAppBuilder _Builder = builder ?? throw new ArgumentNullException(nameof(builder));

    public IServiceCollection Services => _Builder.Services;

    public IConfiguration Configuration => _Builder.Configuration;

    public void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null) where TBuilder : notnull
    {
        _Builder.ConfigureContainer(factory, configure);
    }
}