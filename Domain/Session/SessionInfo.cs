#nullable enable
using System;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session;

public class SessionInfo
{
    public SessionInfo(){}
    /// <summary>
    /// 初始化新实例。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="user"></param>
    public SessionInfo(string? key, DomainUser? user)
    {
        Key = key.HasValue() ? key : Guid.NewGuid().ToString();
        User = user;
        TimeLastActivated = TimeCreated = DateTime.Now;
    }

    public SessionInfo(DomainUser user) : this(null, user) { }

    /// <summary>
    /// 缓存项的 Key
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// 缓存的值
    /// </summary>
    public DomainUser? User { get; internal set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime TimeCreated { get; set; }

    /// <summary>
    /// 最后一次激活时间
    /// </summary>
    public DateTime TimeLastActivated { get; private set; }

    /// <summary>
    /// 发呆时间
    /// </summary>
    public TimeSpan Idle => DateTime.Now - TimeLastActivated;

    /// <summary>
    /// 更新最后激活时间（每次访问时使用）
    /// </summary>
    internal SessionInfo Active()
    {
        TimeLastActivated = DateTime.Now;
        return this;
    }

    /// <summary>
    /// 绑定用户（创建新会话时使用）
    /// </summary>
    internal void BindUser(DomainUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        User = user;
    }
}