using FreeSql;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.FreeSql;

public class FreeSqlEntityDAC<TEntity>(IFreeSql fsql) : IEntityDAC<TEntity>
    where TEntity : class, IDomainEntity, new()
{
    private IUnitOfWork? _nativeUow;

    private IBaseRepository<TEntity> Repo
    {
        get
        {
            var repository = fsql.GetRepository<TEntity>();
            if (_nativeUow != null)
            {
                repository.UnitOfWork = _nativeUow;
            }
            return repository;
        }
    }

    public IQueryable<TEntity> Query => Repo.Select.AsQueryable();

    public void AttachUow(object uow)
    {
        if (uow is IDomainUnitOfWork domainUow)
            _nativeUow = domainUow.OriginalUow as IUnitOfWork;
        else
            _nativeUow = uow as IUnitOfWork;
    }

    #region [ 异步执行实现 ]

    public async Task<TEntity?> FirstOrDefaultAsync(IQueryable<TEntity> query, CancellationToken ct = default)
    {
        return await query.RestoreToSelect().FirstAsync(ct);
    }

    public async Task<List<TEntity>> ToListAsync(IQueryable<TEntity> query, CancellationToken ct = default)
    {
        return await query.RestoreToSelect().ToListAsync(ct);
    }

    public Task<long> CountAsync(IQueryable<TEntity> query, CancellationToken ct = default)
    {
        return query.RestoreToSelect().CountAsync(ct);
    }

    #endregion

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct = default)
    {
        await Repo.InsertAsync(entity, ct);
        return entity;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken ct = default) => Repo.UpdateAsync(entity, ct);

    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default)
        => await Repo.DeleteAsync(entity, ct) > 0;
}