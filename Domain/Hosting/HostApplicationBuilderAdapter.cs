using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting; // 建议放在核心包中

/// <summary>
/// 通用主机构建器适配器（适配 WebApplicationBuilder, HostApplicationBuilder 等）
/// </summary>
public class HostApplicationBuilderAdapter(IHostApplicationBuilder builder) : IDomainAppBuilderAdapter
{
    private readonly IHostApplicationBuilder _Builder = builder ?? throw new ArgumentNullException(nameof(builder));

    public IServiceCollection Services => _Builder.Services;

    public IConfiguration Configuration => _Builder.Configuration;

    public void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null) where TBuilder : notnull
    {
        _Builder.ConfigureContainer(factory, configure);
    }
}