namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 扩展适配器接口：增加对宿主构建（Build）的支持
/// </summary>
public interface IHostApplicationBuilderAdapter : IDomainAppBuilderAdapter
{
    /// <summary>
    /// 触发底层宿主构建（例如调用 HostApplicationBuilder.Build()）
    /// </summary>
    void Build();
}