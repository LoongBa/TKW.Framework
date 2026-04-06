using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web.Hosting;

public class WebApplicationBuilderAdapter(WebApplicationBuilder builder) : IDomainAppBuilderAdapter
{
    private readonly WebApplicationBuilder _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    public IServiceCollection Services => _builder.Services;
    public IConfiguration Configuration => _builder.Configuration;
    public void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null) where TBuilder : notnull
        => ((IHostApplicationBuilder)_builder).ConfigureContainer(factory, configure);

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