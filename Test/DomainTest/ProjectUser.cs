using System;
using System.Security.Principal;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;

namespace DomainTest
{
    /// <summary>
    /// 可以使用 DomainUser，项目里不是必须创建新的
    /// </summary>
    public class ProjectUser : DomainUser, ICopyValues<ProjectUser>
    {
        internal ProjectUser(Func<DomainHost> domainHostFactory) : base(domainHostFactory)
        {
        }

        public ProjectUser()
        {
            
        }

        #region Implementation of ICloneable

        public ProjectUser CopyValuesFrom(ProjectUser fromObject)
        {
            //TODO: copy values
            return this.CopySamePropertiesValue(fromObject);
        }

        /// <summary>Creates a new object that is a copy of the current instance.</summary>
        /// <returns>A new object that is a copy of this instance.</returns>
        public new object Clone()
        {
            return new ProjectUser(DomainHostFactory).CopyValuesFrom(this);
        }

        internal new IIdentity SetIdentity(string userName, UserAuthenticationType authenticationType = UserAuthenticationType.Unset, bool isAuthenticated = false)
        {
            return base.SetIdentity(userName, authenticationType, isAuthenticated);
        }

        #endregion
    }
}