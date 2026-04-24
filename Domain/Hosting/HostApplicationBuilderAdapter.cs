using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 适配 .NET 7+ HostApplicationBuilder
/// </summary>
public class HostApplicationBuilderAdapter<TUserInfo>(HostApplicationBuilder builder)
    : IHostApplicationBuilderAdapter
    where TUserInfo : class, IUserInfo, new()
{
    public IServiceCollection Services => builder.Services;
    public IConfiguration Configuration => builder.Configuration;

    public void Build()
    {
        // 触发宿主构建
        var host = builder.Build();

        // 关键：将构建后的 ServiceProvider 回传给领域主机
        if (DomainHost<TUserInfo>.Root != null)
        {
            // 通过获取单例初始化器来触发回调
            var sp = host.Services;
            // 此时框架会自动执行 ServiceProviderBuiltCallback
        }
    }
}