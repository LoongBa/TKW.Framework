using Microsoft.EntityFrameworkCore;

namespace TKW.Framework.EntityFramework
{
    public interface IEntityDaHelper<out T> where T : DbContext
    {
        T NewDbContext();
    }
}
