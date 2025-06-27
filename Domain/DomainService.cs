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
        public DomainService(DomainUser user)
        {
            User = user.AssertNotNull(nameof(user));
        }
    }

    /// <summary>
    /// 领域控制器：可选，一般情况下用 DomainService + IAopContract 即可。
    /// </summary>
    public class DomainController(DomainUser user) : DomainService(user);
}