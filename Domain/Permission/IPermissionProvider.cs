using System.Collections.Generic;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Permission;

public interface IPermissionProvider<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    IReadOnlyList<MenuPermission> RetrieveUserMenuPermissions(DomainUser<TUserInfo> domainUser);
    IReadOnlyList<DataPermission> RetrieveUserDataPermissions(DomainUser<TUserInfo> domainUser);
    IReadOnlyList<FunctionPermission> RetrieveUserFunctionPermissions(DomainUser<TUserInfo> domainUser);
    IReadOnlyList<UiPermission> RetrieveUserUiPermissions(DomainUser<TUserInfo> domainUser);
}