using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain
{
    public interface IDomainManager<in TIDomainDbContextFactory>
        where TIDomainDbContextFactory : IDomainDataAccessHelper
    {
        void SetDomainDbContextFactory(TIDomainDbContextFactory dbContextFactory);
    }
    public abstract class AbstractDomainManager<TIDomainDbContextFactory> : IDomainManager<TIDomainDbContextFactory>
        where TIDomainDbContextFactory : IDomainDataAccessHelper
    {
        protected TIDomainDbContextFactory DaHelper;

        protected AbstractDomainManager(TIDomainDbContextFactory dbDataAccessHelper)
        {
            DaHelper = dbDataAccessHelper.AssertNotNull<TIDomainDbContextFactory>(nameof(dbDataAccessHelper));
        }

        public void SetDomainDbContextFactory(TIDomainDbContextFactory dbContextFactory)
        {
            DaHelper = (TIDomainDbContextFactory)dbContextFactory.AssertNotNull(nameof(dbContextFactory));
        }
    }
}