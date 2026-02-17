using System;
using System.Threading;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 独立环境（非 Web）下的用户访问器实现
/// 适用于后台任务、消息队列消费者或单元测试环境。
/// </summary>
public class StandaloneDomainUserAccessor<TUserInfo> : IDomainUserAccessor<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    // 利用 AsyncLocal 存储当前逻辑流中的用户实例
    // 它会自动随 await 异步调用向下流转，且在并发任务间物理隔离
    private static readonly AsyncLocal<DomainUser<TUserInfo>?> _userContext = new();

    /// <summary>
    /// 获取当前异步上下文绑定的领域用户
    /// </summary>
    public DomainUser<TUserInfo> DomainUser =>
        _userContext.Value ?? throw new InvalidOperationException("当前异步上下文中未设置 DomainUser，请确保已通过 SetCurrentUser 显式绑定。");

    /// <summary>
    /// 手动设置当前上下文的用户（通常在后台任务入口处调用）
    /// </summary>
    public static void SetCurrentUser(DomainUser<TUserInfo> user)
    {
        _userContext.Value = user;
    }

    /// <summary>
    /// 清除当前上下文的用户
    /// </summary>
    public static void Clear() => _userContext.Value = null;
}