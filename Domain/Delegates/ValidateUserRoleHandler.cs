using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Delegates
{
    /// <summary>
    /// 用户角色验证委托：用户是否属于指定角色
    /// </summary>
    /// <param name="role">角色</param>
    /// <param name="userIdOrName">用户ID或用户名</param>
    /// <exception cref="UserPrivilegeException"></exception>
    public delegate TR ValidateUserRoleHandler<TR>(TR role, string userIdOrName) where TR : IUserRole;
}