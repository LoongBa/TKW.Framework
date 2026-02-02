#nullable enable
using System;
using System.Text.Json.Serialization;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话信息（record 形式，不可变，专注会话生命周期）
/// </summary>
public record SessionInfo<TUserInfo>(string? Key, DomainUser<TUserInfo>? User)
    where TUserInfo: class, IUserInfo, new()
{
    /// <summary>
    /// 无参构造（供 Json 反序列化或其他场景使用）
    /// </summary>
    [JsonConstructor]
    public SessionInfo() : this(null, null) { }

    /// <summary>
    /// 使用用户构造（可选）
    /// </summary>
    public SessionInfo(DomainUser<TUserInfo> user) : this(null, user) { }

    /// <summary>
    /// 创建时间（不可变，使用 UTC 时间）
    /// </summary>
    public DateTime TimeCreated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 最后一次激活时间（不可变，使用 UTC 时间）
    /// </summary>
    public DateTime TimeLastActivated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 发呆时间（计算属性，不参与序列化）
    /// </summary>
    [JsonIgnore]
    public TimeSpan Idle => DateTime.UtcNow - TimeLastActivated;

    /// <summary>
    /// 激活会话（返回新实例，保持 immutable）
    /// </summary>
    public SessionInfo<TUserInfo> Active() => this with { TimeLastActivated = DateTime.UtcNow };

    /// <summary>
    /// 绑定用户（返回新实例）
    /// </summary>
    public SessionInfo<TUserInfo> BindUser(DomainUser<TUserInfo> user)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.SessionKey = Key ?? throw new InvalidOperationException("会话 Key 不能为空");
        return this with { User = user };
    }
}