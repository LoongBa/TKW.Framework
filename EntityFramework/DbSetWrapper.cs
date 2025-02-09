using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TKW.Framework.EntityFramework
{
    public class DbSetWrapper<T> : IDisposable
        where T : class
    {
        public DbSet<T> DbSet { get; }

        public IQueryable<T> QueryableObject { get; }

        private readonly IEntityDbContext _Context;

        public DbSetWrapper(IEntityDbContext context, Expression<Func<T, bool>> filter = null)
        {
            _Context = context;
            DbSet = context.GetDbSet<T>();

            QueryableObject = filter == null ? DbSet : DbSet.Where(filter);
        }

        #region IDisposable

        public void Dispose()
        {
            _Context.Dispose();
        }

        #endregion
    }
}