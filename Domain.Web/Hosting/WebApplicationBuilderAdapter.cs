using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Web.Hosting;

/// <summary>
/// 适配 ASP.NET Core WebApplicationBuilder
/// </summary>
public class WebApplicationBuilderAdapter(WebApplicationBuilder builder) : IDomainAppBuilderAdapter
{
    public IServiceCollection Services => builder.Services;
    public IConfiguration Configuration => builder.Configuration;
}