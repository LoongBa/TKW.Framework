using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Domain.Permission;

public class UiPermission(
    Operator oper,
    string permissionId,
    string permissionName,
    EnumNoneEnabledDisabled type,
    string? permissionText = null)
    : UserPermissionBase(oper, permissionId, permissionName, type, permissionText)
{
    public EnumNoneEnabledDisabled Type { get; } = type;
}