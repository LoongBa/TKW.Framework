using TKW.Framework.Common.DataType;

namespace TKW.Framework.Domain.Permission
{
    public class UiPermission : UserPermissionBase
    {
        public UiPermission(
            Operator oper,
            string permissionId,
            string permissionName,
            EnumNoneEnabledDisabled type,
            string permissionText = null)
            : base(oper, permissionId, permissionName, type, permissionText)
        {
            Type = type;
        }

        public EnumNoneEnabledDisabled Type { get; }
    }
}