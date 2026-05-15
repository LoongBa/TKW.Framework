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

    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default)
        => await Repo.DeleteAsync(entity, ct) > 0;

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct = default)
    {
        await Repo.InsertAsync(entity, ct);
        return entity;
    }
    public async Task<List<TEntity>> InsertBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        // 1. 尝试直接转换以避免分配，若失败则物化为 List（因为返回值要求是 List）
        var entityList = entities as List<TEntity> ?? entities.ToList();

        if (entityList.Count == 0) return entityList;

        // 2. FreeSql 会自动处理 ID 回填到 entityList 的项中
        await Repo.InsertAsync(entityList, ct);

        return entityList;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken ct = default) => Repo.UpdateAsync(entity, ct);

    public Task UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        return Repo.UpdateAsync(entities, ct);
    }

    public Task<int> UpdateColumnsBatchAsync(
        IEnumerable<TEntity> entities,
        System.Linq.Expressions.Expression<Func<TEntity, object>> columns,
        CancellationToken ct = default)
    {
        // 使用 IFreeSql 原生 API 进行指定列的批量更新
        var update = fsql.Update<TEntity>()
            .SetSource(entities)
            .UpdateColumns(columns);

        // 【关键】：必须手动挂载环境事务，否则此操作会游离于 UOW 之外
        if (_nativeUow != null)
        {
            update.WithTransaction(_nativeUow.GetOrBeginTransaction());
        }

        return update.ExecuteAffrowsAsync(ct);
    }
}