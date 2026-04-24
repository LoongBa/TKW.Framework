using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 默认适配器实现
/// </summary>
internal class DefaultDomainAppBuilderAdapter(IServiceCollection services, IConfiguration configuration) : IDomainAppBuilderAdapter
{
    public IServiceCollection Services => services;
    public IConfiguration Configuration => configuration;
}