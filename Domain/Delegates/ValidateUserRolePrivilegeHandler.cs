using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Delegates
{
    /// <summary>
    /// 用户角色权限验证委托
    /// </summary>
    /// <exception cref="UserPrivilegeException"></exception>
    public delegate TF ValidateUserRolePrivilegeHandler<TF, in TR>(TF function, TR role)
        where TF : IFunction
        where TR : IUserRole;
}