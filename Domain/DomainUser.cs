using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Autofac;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Permission;

namespace TKW.Framework.Domain
{
    public class DomainUser : ClaimsPrincipal, IPrincipal
    {
        public Func<DomainHost> DomainHostFactory { get; }
        public UserAuthenticationType AuthenticationType { get; protected set; }
        public UserPermissionSet Permissions { get; }

        protected DomainUser(Func<DomainHost> domainHostFactory)
        {
            DomainHostFactory = domainHostFactory.AssertNotNull(name: nameof(domainHostFactory));

            _Identity = null;
            UserIdString = string.Empty;
            AuthenticationType = UserAuthenticationType.Unset;
            SessionKey = string.Empty;
            Permissions = new UserPermissionSet();
        }

        public TDomainController Use<TDomainController>() where TDomainController : IDomainService
        {
            return DomainHostFactory().AssertNotNull(name: nameof(DomainHostFactory)).Container.Resolve<TDomainController>(TypedParameter.From(this)); 
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

        public List<string> RoleNameStringArray { get; set; } = new List<string>();

        #endregion

        private IIdentity _Identity;

        public new IIdentity Identity
        {
            get => _Identity;
            protected set => _Identity = value; // 允许在派生类中被改写
        }

        protected IIdentity SetIdentity(string userName, UserAuthenticationType authenticationType, bool isAuthenticated)
        {
            _Identity = new CommonIdentity(userName, authenticationType.ToString(), isAuthenticated);
            return _Identity;
        }

        protected IIdentity CopyIdentity(IIdentity identity)
        {
            _Identity = new CommonIdentity(identity.Name, identity.AuthenticationType, identity.IsAuthenticated);
            return _Identity;
        }

        protected IIdentity ToIdentity()
        {
            return new CommonIdentity(Identity.Name, Identity.AuthenticationType, Identity.IsAuthenticated);
        }

        #region Implementation of IUser

        public string UserIdString { get; set; }
        public string SessionKey { get; set; }

        #endregion
    }
}