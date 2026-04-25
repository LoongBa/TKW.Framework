using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域会话作用域：将 DI 作用域与用户会话深度绑定。
/// 实现 IAsyncDisposable 以适配 await using 语法。
/// </summary>
public sealed class DomainSessionScope<TUserInfo> : IAsyncDisposable
    where TUserInfo : class, IUserInfo, new()
{
    private readonly IServiceScope? _scope; // 可为空，若外部提供则不负责释放
    private readonly bool _ownsScope;
    public DomainUser<TUserInfo> User { get; }

    // 内部构造：用于框架自动创建作用域
    internal DomainSessionScope(IServiceScope scope, DomainUser<TUserInfo> user)
    {
        _scope = scope;
        _ownsScope = true;
        User = user;
        DomainUser<TUserInfo>.BindScope(_scope.ServiceProvider); //
    }

    // 内部构造：用于重用外部作用域（如 Web RequestServices）
    internal DomainSessionScope(IServiceProvider provider, DomainUser<TUserInfo> user)
    {
        _scope = null;
        _ownsScope = false;
        User = user;
        DomainUser<TUserInfo>.BindScope(provider); //
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            // 无论哪种模式，退出业务块都必须解除异步上下文绑定
            DomainUser<TUserInfo>.UnBindScope(); //

            // 只有自己创建的作用域才负责物理释放
            if (_ownsScope)
            {
                _scope?.Dispose();
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }
}