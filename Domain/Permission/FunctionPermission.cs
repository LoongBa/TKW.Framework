using TKW.Framework.DataTypes;
using TKW.Framework.Enumerations;

namespace TKW.Framework.Domain.Permission;

public class FunctionPermission : UserPermissionBase
{
    /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
    public FunctionPermission(Operator oper, string permissionId, string permissionName, EnumNoneEnabledDisabled type, string? permissionText = null)
        : base(oper, permissionId, permissionName, type, permissionText)
    {
        Type = type;
    }
    public EnumNoneEnabledDisabled Type { get; }
}