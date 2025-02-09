using Microsoft.EntityFrameworkCore;
using TKW.Framework.EntityFramework;

namespace TKW.Framework.Domain
{
    public interface IDomainDataAccessHelper
    {
        TDbContext CreateDbContextInstance<TDbContext>()
            where TDbContext : DbContext, IEntityDbContext;
    }
}
