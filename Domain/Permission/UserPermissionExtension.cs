using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Permission;

public static class UserPermissionExtension
{
    #region 用户权限

    extension<T>(IReadOnlyList<T> left) where T : IUserPermission
    {
        public bool ContainsByName(string name)
        {
            name.EnsureHasValue(nameof(name));
            return left.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsById(string idString)
        {
            idString.EnsureHasValue(nameof(idString));
            return left.Any(p => p.Id.Equals(idString, StringComparison.OrdinalIgnoreCase));
        }

        public T? TryGetByName(string name)
        {
            name.EnsureHasValue(nameof(name));
            return left.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public T? TryGetById(string idString)
        {
            idString.EnsureHasValue(nameof(idString));
            return left.FirstOrDefault(p => p.Id.Equals(idString, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region 菜单权限

    extension<T>(IReadOnlyList<T> left) where T : MenuPermission
    {
        public MenuPermission? TryGetParentByName(string name)
        {
            name.EnsureHasValue(nameof(name));
            var menu = left.TryGetByName(name);
            if (menu == null || !menu.ParentId.HasValue()) return null;
            return left.FirstOrDefault(p => p.Id.Equals(menu.ParentId, StringComparison.OrdinalIgnoreCase));
        }

        public MenuPermission? TryGetParentById(string idString)
        {
            idString.EnsureHasValue(nameof(idString));
            var menu = left.TryGetById(idString);
            if (menu == null || !menu.ParentId.HasValue()) return null;
            return left.FirstOrDefault(p => p.Id.Equals(menu.ParentId, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

}