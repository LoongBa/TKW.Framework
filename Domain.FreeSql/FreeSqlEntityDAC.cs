using FreeSql; // 必须引入以支持扩展方法
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

    /// <summary>将 ISelect`1 转换为 IQueryable`1 需要 FreeSql 的 Linq 扩展</summary>
    public IQueryable<TEntity> Query => Repo.Select.AsQueryable();

    public void AttachUow(object uow)
    {
        if (uow is IDomainUnitOfWork domainUow)
            _nativeUow = domainUow.OriginalUow as IUnitOfWork;
        else
            _nativeUow = uow as IUnitOfWork;
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct = default)
    {
        await Repo.InsertAsync(entity, ct);
        return entity;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken ct = default) => Repo.UpdateAsync(entity, ct);

    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default)
        => await Repo.DeleteAsync(entity, ct) > 0;
}