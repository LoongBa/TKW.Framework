using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TKW.Framework.Domain.Interfaces;

public interface IEntityReadOnlyDAC<out TEntity> where TEntity : class, IDomainEntity, new()
{
    IQueryable<TEntity> Query { get; }
    // 允许附加工作单元（Object 屏蔽具体 ORM 类型）
    void AttachUow(object uow);
}

/// <summary>
/// 实体级数据访问契约 (ORM 无关)
/// </summary>
public interface IEntityDAC<TEntity> : IEntityReadOnlyDAC<TEntity> where TEntity : class, IDomainEntity, new()
{
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default);
}