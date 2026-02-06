using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Permission;

public class MenuPermission : UserPermissionBase
{
    /// <summary>
    /// 父级菜单Id
    /// </summary>
    public string ParentId { get; }

    /// <summary>
    /// 菜单显示顺序
    /// </summary>
    public int DisplayOrder { get; }
    /// <summary>
    /// 菜单权限类型
    /// </summary>
    public MenuPermissionType Type { get; }

    /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
    public MenuPermission(
        Operator oper,
        string permissionId, string permissionName,
        MenuPermissionType type = MenuPermissionType.Enabled,
        string? menuParentId = null,
        string? permissionText = null,
        int displayOrder = 0) : base(oper, permissionId, permissionName, type, permissionText)
    {
        ParentId = menuParentId.HasNoValueToNull() ?? string.Empty;
        DisplayOrder = displayOrder;
        Type = type;
    }

    public bool IsVisible => Type == MenuPermissionType.Disabled || Type == MenuPermissionType.Enabled;

    public bool IsEnabled => Type == MenuPermissionType.Enabled;
}