using System;
using TKW.Framework.DataTypes;

namespace TKW.Framework.Domain.Permission;

public interface IUserPermission
{
    Operator Operator { get; }
    Enum PermissionType { get; }

    /// <summary>
    /// Id
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Ãû×Ö
    /// </summary>
    string Name { get; }

    /// <summary>
    /// ²Ëµ¥ÎÄ×Ö
    /// </summary>
    string Text { get; }
}