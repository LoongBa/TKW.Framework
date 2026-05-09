using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Entity;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域数据服务只读基类（适用于 View 或只读实体）
/// </summary>
public abstract class DomainReadOnlyDataServiceBase<TUserInfo, TEntity, TDto> : DomainServiceBase<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
    where TEntity : class, IDomainEntity, new()
    where TDto : class, IDomainDto<TEntity>
{
    protected readonly IEntityDAC<TEntity> Dac;
    protected readonly bool HasSoftDelete;
    protected readonly bool HasEnableStatus;

    protected DomainReadOnlyDataServiceBase(DomainUser<TUserInfo> user, IEntityDAC<TEntity> dac,
        bool hasSoftDelete = false, bool hasEnableStatus = false) : base(user)
    {
        Dac = dac;
        Dac.AttachUow(User.GetUow());
        HasSoftDelete = hasSoftDelete;
        HasEnableStatus = hasEnableStatus;
    }

    protected TDto MapToDto(TEntity entity) => entity.ToDto<TDto>();

    #region [ 查询拦截钩子 ]
    protected virtual void OnQueryFiltering(ref IQueryable<TEntity> query) { }
    protected virtual void OnGraphQLFiltering(ref IQueryable<TEntity> query) { }
    #endregion

    #region [ 1. 公共查询接口 ]
    public virtual IQueryable<TEntity> GetGraphQLQueryable()
    {
        var select = QueryForUser();
        OnGraphQLFiltering(ref select);
        return select;
    }

    public virtual async Task<TDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        if (entity == null) throw new EntityNotFoundException(typeof(TEntity).Name, $"Id={id}");
        return MapToDto(entity);
    }

    public virtual async Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
    {
        var q = QueryForUser();
        if (predicate != null) q = q.Where(predicate);
        // 使用 DAC 执行异步计数
        return await Dac.CountAsync(q, ct);
    }

    public virtual async Task<List<TDto>> SelectAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        int skip = 0, int limit = 100,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
        var list = await InternalSelectAsync(predicate, skip, limit, orderBy, ct);
        return list.Select(MapToDto).ToList();
    }

    public virtual async Task<PageResult<TDto>> SelectPageAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        int pageNumber = 1, int pageSize = 100,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
        var count = await CountAsync(predicate, ct);
        var list = await InternalSelectAsync(predicate, (pageNumber - 1) * pageSize, pageSize, orderBy, ct);
        return new PageResult<TDto>(count, list.Select(MapToDto).ToList(), pageNumber, pageSize);
    }
    #endregion

    #region [ 2. 内部原子逻辑 (查询) ]
    protected internal virtual IQueryable<TEntity> QueryForUser()
    {
        var q = Dac.Query;

        if (HasSoftDelete && typeof(IEntitySoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            q = q.Where(x => !((IEntitySoftDelete)x).IsDeleted);
        }

        OnQueryFiltering(ref q);
        return q;
    }

    public virtual async Task<TEntity?> InternalGetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        var q = QueryForUser().Where(predicate);
        // 使用 DAC 执行异步 FirstOrDefault
        var entity = await Dac.FirstOrDefaultAsync(q, ct);
        if (entity != null) entity.IsFromPersistentSource = true;
        return entity;
    }

    protected internal virtual async Task<List<TEntity>> InternalSelectAsync(
        Expression<Func<TEntity, bool>>? predicate, int skip, int limit,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy, CancellationToken ct)
    {
        var safeSkip = Math.Max(0, skip);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var q = QueryForUser();

        if (predicate != null) q = q.Where(predicate);
        q = orderBy != null ? orderBy(q) : q.OrderByDescending(x => x.Id);

        // 使用 DAC 执行异步 List
        var list = await Dac.ToListAsync(q.Skip(safeSkip).Take(safeLimit), ct);
        list.ForEach(e => e.IsFromPersistentSource = true);
        return list;
    }
    #endregion
}