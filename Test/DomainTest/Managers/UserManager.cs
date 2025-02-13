using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using DomainTest.Models;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace DomainTest.Managers
{
    public class UserManager : AbstractDomainManager<DomainTestDataAccessHelper>
    {
        private readonly Dictionary<Guid, User> _Users = new Dictionary<Guid, User>();
        public UserManager(DomainTestDataAccessHelper dbDataAccessHelper) : base(dbDataAccessHelper)
        {
        }

        /// <exception cref="AuthenticationException">Condition.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Condition.</exception>
        public User GetUserByUsername(ProjectUser currentProjectUser, string username)
        {
            if (!currentProjectUser.AssertNotNull().Identity.Name!.Equals("admin", StringComparison.OrdinalIgnoreCase))
                throw new AuthenticationException($"用户 '{currentProjectUser.Identity.Name}' 没有此项操作权限。");

            var user = _Users.FirstOrDefault(u => u.Value.UserName.Equals(username.EnsureHasValue(), StringComparison.OrdinalIgnoreCase)).Value;
            if (user == null) throw new ArgumentOutOfRangeException($"用户名为 '{username}' 的用户不存在。");
            return user;
        }
        public User GetUserByUid(Guid uId)
        {

            var user = _Users.FirstOrDefault(u => u.Value.Uid == uId).Value;
            if (user == null) throw new ArgumentOutOfRangeException($"UID 为 '{uId}' 的用户不存在。");
            return user;
        }
        public List<User> SearchUsers(string nameKeyword)
        {
            nameKeyword.EnsureHasValue();
            var pairs = _Users.Where(u => u.Value.RealName.Contains(nameKeyword)
                                          || u.Value.UserName.Contains(nameKeyword));
            return pairs.Select(pair => pair.Value).ToList();
        }

        public List<User> ListAllUsers()
        {
            return _Users.Select(pair => pair.Value).ToList();
        }
        public List<User> ListAllUsers(ProjectUser projectUser)
        {
            return _Users.Select(pair => pair.Value).ToList();
        }

        /// <exception cref="ArgumentException">Condition.</exception>
        public User AddUser(string userName, string realName, string password)
        {
            userName.EnsureHasValue();
            realName.EnsureHasValue();
            //这里转为16进制
            password = password.EnsureHasValue().ToHexString();

            if (_Users.Count(u => u.Value.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)) > 0)
                throw new ArgumentException($"相同的用户名 '{userName}' 的用户已经存在。");

            var user = new User(userName, password, realName);
            _Users.Add(user.Uid, user);
            return user;
        }
        public void DelUser(ProjectUser currentProjectUser, string userName)
        {
            var user = GetUserByUsername(currentProjectUser, userName);
            _Users.Remove(user.Uid);
        }
        public void DelUser(Guid uid)
        {
            var user = GetUserByUid(uid);
            _Users.Remove(user.Uid);
        }
    }
}