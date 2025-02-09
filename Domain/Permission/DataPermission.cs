using TKW.Framework.Common.DataType;

namespace TKW.Framework.Domain.Permission
{
    public class DataPermission : UserPermissionBase
    {
        public DataPermission(Operator oper, string permissionId, string permissionName, EnumNoneReadOnlyReadWrite type = EnumNoneReadOnlyReadWrite.ReadOnly, string permissionText = null)
            : base(oper, permissionId, permissionName, type, permissionText)
        {
            Type = type;
        }

        public EnumNoneReadOnlyReadWrite Type { get; }

        public bool IsReadOnly => Type == EnumNoneReadOnlyReadWrite.ReadOnly;
        public bool IsWritable => Type == EnumNoneReadOnlyReadWrite.ReadAndWrite;
        public bool IsForbidden => Type == EnumNoneReadOnlyReadWrite.None;
    }
}