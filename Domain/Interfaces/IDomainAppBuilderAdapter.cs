using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// 统一抽象各平台的应用构建器 (屏蔽 WebApplicationBuilder 和 MauiAppBuilder 的差异)
/// </summary>
public interface IDomainAppBuilderAdapter
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
    void ConfigureContainer<TBuilder>(IServiceProviderFactory<TBuilder> factory, Action<TBuilder>? configure = null) where TBuilder : notnull;
}