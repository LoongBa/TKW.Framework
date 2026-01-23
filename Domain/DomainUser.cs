using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Permission;

namespace TKW.Framework.Domain
{
    public class DomainUser() : ClaimsPrincipal, IPrincipal
    {
        public Func<DomainHost> DomainHostFactory { get; private set; }
        public UserAuthenticationType AuthenticationType { get; protected set; } = UserAuthenticationType.Unset;
        public UserPermissionSet Permissions { get; } = new();

        /// <summary>
        /// 领域中的用户信息（改为不用泛型，需自行检查、装箱拆箱）
        /// </summary>
        public IUserInfo UserInfo { get; set; }

        protected DomainUser(Func<DomainHost> domainHostFactory) : this()
        {
            DomainHostFactory = domainHostFactory.AssertNotNull(name: nameof(domainHostFactory));
        }

        protected internal Func<DomainHost> SetDomainHostFactory(Func<DomainHost> domainHostFactory)
        {
            return DomainHostFactory = domainHostFactory.AssertNotNull(name: nameof(domainHostFactory));
        }

        /// <summary>
        /// 使用 DomainService 领域服务
        /// </summary>
        /// <remarks>注意：必须在 DomainHost.Initial() 中注册领域服务</remarks>
        /// <typeparam name="TDomainService">领域服务的接口类型</typeparam>
        public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase
        {
            return DomainHostFactory().AssertNotNull(name: nameof(DomainHostFactory)).Container.Resolve<TDomainService>(TypedParameter.From(this));
        }

        /// <summary>
        /// 使用 DomainService 领域服务 AOP
        /// </summary>
        /// <remarks>注意：必须在 DomainHost.Initial() 中注册领域服务</remarks>
        /// <typeparam name="TAopContract">领域服务的接口类型</typeparam>
        public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
        {
            return DomainHostFactory().AssertNotNull(name: nameof(DomainHostFactory)).Container.Resolve<TAopContract>(TypedParameter.From(this));
        }

        #region IPrincipal 接口的方法

        /// <summary>
        /// 角色判断
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public override bool IsInRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentNullException(nameof(role));
            return RoleNameStringArray.Any(r => r.Equals(role, StringComparison.CurrentCultureIgnoreCase));
        }

        public List<string> RoleNameStringArray { get; set; } = [];

        #endregion

        private IIdentity _Identity = new CommonIdentity("", "", false);

        public new IIdentity Identity
        {
            get => _Identity;
            protected set => _Identity = value; // 允许在派生类中被改写
        }

        protected internal IIdentity SetIdentity(string userName, string authenticationType, bool isAuthenticated)
        {
            return _Identity = new CommonIdentity(
                userName.EnsureHasValue().TrimSelf(),
                authenticationType.EnsureHasValue().TrimSelf(),
                isAuthenticated);
        }

        protected IIdentity ToIdentity()
        {
            return new CommonIdentity(_Identity.Name, _Identity.AuthenticationType, _Identity.IsAuthenticated);
        }

        #region Implementation of IUser

        public string UserIdString { get; set; } = string.Empty;
        public string SessionKey { get; set; } = string.Empty;

        #endregion
    }
}