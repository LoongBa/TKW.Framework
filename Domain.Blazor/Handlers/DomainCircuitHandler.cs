using Microsoft.AspNetCore.Components.Server.Circuits;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Blazor.Handlers;

/// <summary>
/// 领域电路处理器基类：全生命周期自动管理
/// </summary>
public abstract class DomainCircuitHandlerBase<TUserInfo>(IServiceProvider serviceProvider) : CircuitHandler
    where TUserInfo : class, IUserInfo, new()
{
    protected readonly IServiceProvider ServiceProvider = serviceProvider;

    /// <summary>电路生命周期 (逻辑层)：电缆接入：最先执行</summary>
    public sealed override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        DomainUser<TUserInfo>.BindScope(ServiceProvider);
        await OnDomainCircuitOpenedAsync(circuit, cancellationToken);
        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    /// <summary>电路生命周期 (逻辑层)：电缆切断：最后执行</summary>
    public sealed override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        try
        {
            await OnDomainCircuitClosedAsync(circuit, cancellationToken);
        }
        finally
        {
            // 电缆切断：最后清理
            DomainUser<TUserInfo>.UnBindScope();
        }
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    /// <summary>连接生命周期 (物理层)：链路恢复：重新加固插座绑定</summary>
    public sealed override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // 应对断网重连后，线程上下文切换可能导致的 AsyncLocal 丢失
        DomainUser<TUserInfo>.BindScope(ServiceProvider);
        await OnDomainConnectionUpAsync(circuit, cancellationToken);
        await base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    /// <summary>连接生命周期 (物理层)：链路中断：清理当前线程上下文，防止线程回池后的污染</summary>
    public sealed override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        try
        {
            await OnDomainConnectionDownAsync(circuit, cancellationToken);
        }
        finally
        {
            // 链路中断：清理当前线程上下文，防止线程回池后的污染
            DomainUser<TUserInfo>.UnBindScope();
        }
        await base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    // --- 3. 模板方法模式：供子类使用的受保护钩子 ---
    protected virtual Task OnDomainCircuitOpenedAsync(Circuit circuit, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnDomainCircuitClosedAsync(Circuit circuit, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnDomainConnectionUpAsync(Circuit circuit, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnDomainConnectionDownAsync(Circuit circuit, CancellationToken ct) => Task.CompletedTask;
}