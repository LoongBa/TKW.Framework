using System;
using System.Collections.Generic;
using System.Security.Authentication;
using DomainTest.Managers;
using DomainTest.Services.Contracts;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace DomainTest.Services
{
    public class UserController(DomainUser domainUser, UserManager userManager)
        : DomainService(domainUser), IUserControllerContract
    {
        private readonly UserManager _UserManager = userManager.AssertNotNull();
        //var userManager = new UserManager();

        public List<User> ListAllUsers1()
        {
            //领域过滤器 判断用户权限
            return _UserManager.ListAllUsers();
            //领域过滤器 记录访问日志
        }
        public List<User> ListAllUsers2()
        {
            //领域方法判断用户权限
            if (!DomainUser.Identity.Name.Equals("Guest", StringComparison.OrdinalIgnoreCase))
                throw new AuthenticationException($"用户 '{DomainUser.Identity.Name}' 没有此项操作权限。");
            return _UserManager.ListAllUsers();
            //TODO: 记录访问日志
        }

        public List<User> ListAllUsers3()
        {
            //业务方法内部判断用户权限
            //TODO: 记录访问日志
            return _UserManager.ListAllUsers(DomainUser as ProjectUser);
            //TODO: 记录访问日志
        }

        public List<User> SearchUsers(string nameKeyword)
        {
            //领域方法判断用户权限
            if (!DomainUser.Identity.Name.Equals("Guest", StringComparison.OrdinalIgnoreCase))
                throw new AuthenticationException($"用户 '{DomainUser.Identity.Name}' 没有此项操作权限。");
            return _UserManager.SearchUsers(nameKeyword);
        }

        public User GetUserByUid(Guid uid)
        {
            //领域过滤器 判断用户权限
            return _UserManager.GetUserByUid(uid);
        }

        public User GetUserByUsername(string username)
        {
            //业务方法内部判断用户权限
            return _UserManager.GetUserByUsername(DomainUser as ProjectUser, username);
        }

        public void DelUserByUid(Guid uid)
        {
            base.DomainUser.Use<UserManager>().AddUser()
            //领域方法判断用户权限
            if (!DomainUser.Identity.Name.Equals("admin", StringComparison.OrdinalIgnoreCase))
                throw new AuthenticationException($"用户 '{DomainUser.Identity.Name}' 没有此项操作权限。");
            _UserManager.DelUser(uid);
        }

        public void DelUserByUsername(string username)
        {
            //业务方法内部判断用户权限
            _UserManager.DelUser(DomainUser as ProjectUser, username);
        }
    }
}