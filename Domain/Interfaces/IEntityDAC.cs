using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// 实体级数据访问只读契约
/// </summary>
public interface IEntityReadOnlyDAC<TEntity> where TEntity : class, IDomainEntity, new()
{
    IQueryable<TEntity> Query { get; }

    // 允许附加工作单元（Object 屏蔽具体 ORM 类型）
    void AttachUow(object uow);

    // 新增：由具体的 DAC 实现决定如何异步执行 IQueryable
    Task<TEntity?> FirstOrDefaultAsync(IQueryable<TEntity> query, CancellationToken ct = default);
    Task<List<TEntity>> ToListAsync(IQueryable<TEntity> query, CancellationToken ct = default);
    Task<long> CountAsync(IQueryable<TEntity> query, CancellationToken ct = default);
}

/// <summary>
/// 实体级数据访问契约 (ORM 无关)
/// </summary>
public interface IEntityDAC<TEntity> : IEntityReadOnlyDAC<TEntity> where TEntity : class, IDomainEntity, new()
{
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct = default);
    Task<List<TEntity>> InsertBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    /// <summary>指定列的批量更新（性能极佳，按需更新）</summary>
    Task<int> UpdateColumnsBatchAsync(
        IEnumerable<TEntity> entities,
        System.Linq.Expressions.Expression<Func<TEntity, object>> columns,
        CancellationToken ct = default);
}