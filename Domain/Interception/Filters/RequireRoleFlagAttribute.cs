using System;
using System.Linq;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 要求指定角色才能访问的标记属性。
/// 支持多组标记叠加，组内逻辑可配置（Any/All）。
/// </summary>
/// <remarks>
/// 推荐写法（支持枚举）：
/// [RequireRoleFlag(UserRole.Admin, UserRole.SuperUser)]
/// [RequireRoleFlag(RoleLogic.All, "Finance", "Audit")]
/// </remarks>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public class RequireRoleFlagAttribute : DomainFlagAttribute
{
    /// <summary>
    /// 需要的角色列表（统一存储为字符串以兼容 Claims 机制）
    /// </summary>
    public string[] Roles { get; }

    /// <summary>
    /// 多个角色之间的逻辑关系
    /// </summary>
    public RoleLogic Logic { get; }

    /// <summary>
    /// 默认构造：满足任一角色即可
    /// </summary>
    public RequireRoleFlagAttribute(params object[] roles)
        : this(RoleLogic.Any, roles)
    {
    }

    /// <summary>
    /// 全功能构造
    /// </summary>
    /// <param name="logic">逻辑关系（Any/All）</param>
    /// <param name="roles">角色列表（支持 string 或 Enum）</param>
    public RequireRoleFlagAttribute(RoleLogic logic, params object[]? roles)
    {
        Logic = logic;

        // 优化方案：一步到位过滤 null、无效字符串及重复项
        // 这样既保证了 non-nullable 的 string[] 结果，也确保了安全性
        Roles = roles?
            .Select(r => r.ToString())
            .OfType<string>() // 过滤 null 并转换类型为 IEnumerable<string>
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .ToArray() ?? [];

        if (Roles.Length == 0)
            throw new ArgumentException("权限标记至少需要指定一个有效的角色名称或枚举值。", nameof(roles));
    }
}

/// <summary>
/// 角色组合的逻辑关系策略
/// </summary>
public enum RoleLogic
{
    /// <summary>
    /// 满足任一角色即可（逻辑或 OR）
    /// </summary>
    Any,

    /// <summary>
    /// 必须全部满足（逻辑与 AND）
    /// </summary>
    All
}