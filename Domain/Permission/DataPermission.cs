using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Domain.Permission;

public class DataPermission(
    Operator oper,
    string permissionId,
    string permissionName,
    EnumNoneReadOnlyReadWrite type = EnumNoneReadOnlyReadWrite.ReadOnly,
    string? permissionText = null)
    : UserPermissionBase(oper, permissionId, permissionName, type, permissionText)
{
    public EnumNoneReadOnlyReadWrite Type { get; } = type;

    public bool IsReadOnly => Type == EnumNoneReadOnlyReadWrite.ReadOnly;
    public bool IsWritable => Type == EnumNoneReadOnlyReadWrite.ReadAndWrite;
    public bool IsForbidden => Type == EnumNoneReadOnlyReadWrite.Unset;
}