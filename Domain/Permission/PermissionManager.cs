using System.Collections.Generic;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Permission;

public class PermissionManager<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly IPermissionProvider<TUserInfo> _Provider;
    public PermissionManager(IPermissionProvider<TUserInfo> provider)
    {
        provider.EnsureNotNull(name: nameof(provider));
        _Provider = provider;
    }

    public TUserInfo RetrieveUserPermissions(TUserInfo user)
    {
        RetrieveUserDataPermissions(user);
        RetrieveUserFunctionPermissions(user);
        RetrieveUserMenuPermissions(user);
        RetrieveUserUiPermissions(user);
        return user;
    }

    public TUserInfo RetrieveUserMenuPermissions(TUserInfo user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Menus = _Provider.RetrieveUserMenuPermissions(user) ?? new List<MenuPermission>();
        return user;
    }
    public TUserInfo RetrieveUserDataPermissions(TUserInfo user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Datas = _Provider.RetrieveUserDataPermissions(user) ?? new List<DataPermission>();
        return user;
    }
    public TUserInfo RetrieveUserFunctionPermissions(TUserInfo user)
    {
        //通过 Provider 加载数据

        //user.Permissions.Functions = _Provider.RetrieveUserFunctionPermissions(user) ?? new List<FunctionPermission>();
        return user;
    }
    public TUserInfo RetrieveUserUiPermissions(TUserInfo user)
    {
        //通过 Provider 加载数据
        //user.Permissions.Uis = _Provider.RetrieveUserUiPermissions(user) ?? new List<UiPermission>();
        return user;
    }
}