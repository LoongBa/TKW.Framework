using System.Collections.Generic;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Permission;

public class PermissionManager<T>
    where T : DomainUser
{
    private readonly IPermissionProvider _Provider;
    public PermissionManager(IPermissionProvider provider)
    {
        provider.EnsureNotNull(name: nameof(provider));
        _Provider = provider;
    }

    public T RetrieveUserPermissions(T user)
    {
        RetrieveUserDataPermissions(user);
        RetrieveUserFunctionPermissions(user);
        RetrieveUserMenuPermissions(user);
        RetrieveUserUiPermissions(user);
        return user;
    }

    public T RetrieveUserMenuPermissions(T user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Menus = _Provider.RetrieveUserMenuPermissions(user) ?? new List<MenuPermission>();
        return user;
    }
    public T RetrieveUserDataPermissions(T user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Datas = _Provider.RetrieveUserDataPermissions(user) ?? new List<DataPermission>();
        return user;
    }
    public T RetrieveUserFunctionPermissions(T user)
    {
        //通过 Provider 加载数据

        //user.Permissions.Functions = _Provider.RetrieveUserFunctionPermissions(user) ?? new List<FunctionPermission>();
        return user;
    }
    public T RetrieveUserUiPermissions(T user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Uis = _Provider.RetrieveUserUiPermissions(user) ?? new List<UiPermission>();
        return user;
    }
}