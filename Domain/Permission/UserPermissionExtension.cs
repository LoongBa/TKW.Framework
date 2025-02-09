using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Permission
{
    public static class UserPermissionExtension
    {
        #region Ȩ���жϻ�������

        public static bool ContainsByName<T>(this IReadOnlyList<T> left, string name) where T : IUserPermission
        {
            name.EnsureHasValue(nameof(name));
            return left.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public static bool ContainsById<T>(this IReadOnlyList<T> left, string idString) where T : IUserPermission
        {
            idString.EnsureHasValue(nameof(idString));
            return left.Any(p => p.Id.Equals(idString, StringComparison.OrdinalIgnoreCase));
        }
        public static T TryGetByName<T>(this IReadOnlyList<T> left, string name) where T : IUserPermission
        {
            name.EnsureHasValue(nameof(name));
            return left.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public static T TryGetById<T>(this IReadOnlyList<T> left, string idString) where T : IUserPermission
        {
            idString.EnsureHasValue(nameof(idString));
            return left.FirstOrDefault(p => p.Id.Equals(idString, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region �˵����

        public static MenuPermission TryGetParentByName<T>(this IReadOnlyList<T> left, string name) where T : MenuPermission
        {
            name.EnsureHasValue(nameof(name));
            var menu = left.TryGetByName(name);
            if (!menu.ParentId.HasValue()) return null;
            return left.FirstOrDefault(p => p.Id.Equals(menu.ParentId, StringComparison.OrdinalIgnoreCase));
        }
        public static MenuPermission TryGetParentById<T>(this IReadOnlyList<T> left, string idString) where T : MenuPermission
        {
            idString.EnsureHasValue(nameof(idString));
            var menu = left.TryGetById(idString);
            if (!menu.ParentId.HasValue()) return null;
            return left.FirstOrDefault(p => p.Id.Equals(menu.ParentId, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

    }
}