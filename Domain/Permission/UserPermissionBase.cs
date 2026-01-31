using System;
using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Permission;

public class UserPermissionBase : IUserPermission
{
    public Operator Operator { get; }
    public Enum PermissionType { get; }

    /// <inheritdoc />
    /// <summary>
    /// Id
    /// </summary>
    public string Id { get; }

    /// <inheritdoc />
    /// <summary>
    /// 名字
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    /// <summary>
    /// 菜单文字
    /// </summary>
    public string Text { get; }

    /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
    public UserPermissionBase(Operator oper, string permissionId, string permissionName, Enum type, string permissionText = null)
    {
        oper.EnsureNotNull(name: nameof(oper));
        permissionId.EnsureHasValue(nameof(permissionId));
        permissionName.EnsureHasValue(nameof(permissionName));

        Operator = oper;
        Id = permissionId;
        Name = permissionName;
        Text = permissionText ?? Name;

        PermissionType = type;
    }
}