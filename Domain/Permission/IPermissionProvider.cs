using System.Collections.Generic;

namespace TKW.Framework.Domain.Permission;

public interface IPermissionProvider
{
    IReadOnlyList<MenuPermission> RetrieveUserMenuPermissions(DomainUser domainUser);
    IReadOnlyList<DataPermission> RetrieveUserDataPermissions(DomainUser domainUser);
    IReadOnlyList<FunctionPermission> RetrieveUserFunctionPermissions(DomainUser domainUser);
    IReadOnlyList<UiPermission> RetrieveUserUiPermissions(DomainUser domainUser);
}