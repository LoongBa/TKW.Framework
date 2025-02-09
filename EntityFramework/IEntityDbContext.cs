using System;
using Microsoft.EntityFrameworkCore;
using TKW.Framework.Common.Entity.Interfaces;

namespace TKW.Framework.EntityFramework
{
    public interface IEntityDbContext: IDisposable
    {
        DbSet<TEntityModel> GetDbSet<TEntityModel>() where TEntityModel : class, IEntityModel;
    }
}
