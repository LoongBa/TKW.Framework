using Microsoft.AspNetCore.Components.Server.Circuits;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Blazor.Handlers;

/// <summary>
/// 领域电路处理器：适配 Blazor Server 的长连接生命周期
/// </summary>
public class DomainCircuitHandler<TUserInfo>(
    IServiceProvider serviceProvider,
    DomainHost<TUserInfo> domainHost) : CircuitHandler
    where TUserInfo : class, IUserInfo, new()
{
    // 电路开启：相当于中间件的入口
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // 绑定当前电路的 Scoped 容器到异步插座
        DomainUser<TUserInfo>.BindScope(serviceProvider);
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    // 电路关闭：相当于中间件的 finally 块
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // 物理清理：拔掉插座，防止线程污染
        DomainUser<TUserInfo>.UnBindScope();
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}