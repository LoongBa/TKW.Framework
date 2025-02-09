using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain
{
    public class DomainService : IDomainService
    {
        public DomainUser DomainUser { get; }

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public DomainService(DomainUser domainUser)
        {
            DomainUser = domainUser.AssertNotNull(nameof(domainUser));
        }
    }
}