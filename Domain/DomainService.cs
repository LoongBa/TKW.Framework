using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain
{
    /// <summary>
    /// 领域服务
    /// </summary>
    public class DomainService : IDomainService
    {
        protected internal DomainUser User { get; }
        protected T Use<T>() where T : IDomainService
        {
            return User.Use<T>();
        }
        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public DomainService(DomainUser domainUser)
        {
            User = domainUser.AssertNotNull(nameof(domainUser));
        }
    }

    /// <summary>
    /// 领域控制器
    /// </summary>
    public class DomainController(DomainUser domainUser) : DomainService(domainUser);
}