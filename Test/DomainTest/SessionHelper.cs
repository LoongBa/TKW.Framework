using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace DomainTest
{
    public class SessionHelper : SessionHelperBase<ProjectUser>
    {
        /// <inheritdoc />
        public SessionHelper(Func<DomainHost> hostFactory) : base(hostFactory) { }

        #region Overrides of DomainServerBase<ProjectUser>

        protected override ProjectUser CreateUserInstance()
        {
            return new ProjectUser(DomainHostFactory);
        }

        /// <summary>
        /// 返回新的 Guest 用户
        /// </summary>
        protected override ProjectUser OnGuestUserLogin()
        {
            var user = CreateUserInstance();
            user.SetIdentity("Guest", UserAuthenticationType.Tester); //匿名用户
            return user;
        }

        /// <summary>
        /// 用户登录并返回用户
        /// </summary>
        protected override ProjectUser OnUserLogin(string userName, string passWordHashed, UserAuthenticationType authType)
        {
            var user = CreateUserInstance();
            //TODO: 替换成验证用户名密码的业务逻辑
            var isAuthenticated = userName.HasValue() && passWordHashed.HasValue(); 

            user.SetIdentity(userName, UserAuthenticationType.Tester, isAuthenticated);
            return user;
        }

        #endregion
    }
}
