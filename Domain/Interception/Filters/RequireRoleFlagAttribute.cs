using System;
using System.Linq;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 要求指定角色才能访问（标记类）
/// </summary>
/// <remarks>
/// 使用方式：
/// [RequireRoleFlag("admin")]
/// [RequireRoleFlag("manager", "supervisor")]
/// [RequireRoleFlag(RoleLogic.All, "admin", "finance")]
/// 支持多个标记叠加，默认使用 Any 逻辑
/// </remarks>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public class RequireRoleFlagAttribute : DomainFlagAttribute
{
    /// <summary>
    /// 需要的角色列表
    /// </summary>
    public string[] Roles { get; }

    /// <summary>
    /// 多个角色之间的逻辑关系（默认：Any）
    /// </summary>
    public RoleLogic Logic { get; }

    /// <summary>
    /// 兼容旧写法，默认使用 Any 逻辑
    /// </summary>
    public RequireRoleFlagAttribute(params string[] roles)
        : this(RoleLogic.Any, roles)
    {
    }

    public RequireRoleFlagAttribute(RoleLogic logic, params string[]? roles)
    {
        Logic = logic;
        Roles = roles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray()
                ?? [];

        if (Roles.Length == 0)
            throw new ArgumentException("至少需要指定一个角色", nameof(roles));
    }
}

/// <summary>
/// 角色组合的逻辑关系
/// </summary>
public enum RoleLogic
{
    /// <summary>
    /// 满足任一角色即可（OR）
    /// </summary>
    Any,

    /// <summary>
    /// 必须全部满足（AND）
    /// </summary>
    All
}