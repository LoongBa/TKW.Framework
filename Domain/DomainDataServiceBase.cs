using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;

namespace TKW.Framework.Domain;
/// <summary>
/// 领域数据服务基类
/// 用于实现基于领域实体的标准 CRUD 业务服务的抽象基类，
/// 提供了公共的写操作实现和可重写的查询过滤钩子，
/// 以及内部的原子级操作方法，适合大多数简单数据表的业务逻辑封装。
/// </summary>
public abstract class DomainDataServiceBase<TUserInfo, TEntity, TDto> : DomainServiceBase<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
    where TEntity : class, IDomainEntity, new()
    where TDto : class, IDomainDto<TEntity>
{
    protected readonly IEntityDAC<TEntity> _dac;

    protected DomainDataServiceBase(DomainUser<TUserInfo> user, IEntityDAC<TEntity> dac) : base(user)
    {
        _dac = dac;
        _dac.AttachUow(User.GetUow());
    }

    protected abstract TDto MapToDto(TEntity entity);

    #region [ 拦截钩子 - 虚方法 ]

    /// <summary>
    /// 查询过滤钩子（用于实现多租户、逻辑删除等自动过滤）
    /// </summary>
    protected virtual void OnQueryFiltering(ref IQueryable<TEntity> query) { }

    protected virtual void OnBeforeCreate(TEntity entity) { }
    protected virtual void OnAfterCreate(TEntity entity) { }

    protected virtual void OnBeforeUpdate(TEntity entity) { }
    protected virtual void OnAfterUpdate(TEntity entity) { }

    protected virtual void OnBeforeDelete(TEntity entity) { }
    protected virtual void OnAfterDelete(TEntity entity) { }

    #endregion

    #region [ 公共写操作 ]

    public virtual async Task<TDto> CreateAsync(TDto dto, CancellationToken ct = default)
    {
        dto.ValidateData(EnumSceneFlags.Create);
        var entity = dto.ToEntity(EnumSceneFlags.Create);
        var result = await InternalCreateAsync(entity, ct);
        return MapToDto(result);
    }

    public virtual async Task UpdateAsync(long id, TDto dto, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        if (entity == null) throw new EntityNotFoundException(typeof(TEntity).Name, $"Id={id}");

        dto.ValidateData(EnumSceneFlags.Update);
        dto.ApplyToEntity(entity, EnumSceneFlags.Update);
        await InternalUpdateAsync(entity, ct);
    }

    public virtual Task<bool> DeleteAsync(long id, CancellationToken ct = default)
        => InternalHardDeleteAsync(id, ct);

    #endregion

    #region [ 内部原子逻辑 ]

    protected internal virtual IQueryable<TEntity> QueryForUser()
    {
        var q = _dac.Query;
        OnQueryFiltering(ref q);
        return q;
    }

    internal virtual async Task<TEntity?> InternalGetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        var entity = await QueryForUser().Where(predicate).FirstOrDefaultAsync(ct);
        if (entity != null) entity.IsFromPersistentSource = true;
        return entity;
    }

    internal virtual async Task<TEntity> InternalCreateAsync(TEntity entity, CancellationToken ct = default)
    {
        OnBeforeCreate(entity);
        entity.Validate(EnumSceneFlags.Create | EnumSceneFlags.ForceValidate);
        await _dac.InsertAsync(entity, ct);
        OnAfterCreate(entity);
        return entity;
    }

    internal virtual async Task InternalUpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        OnBeforeUpdate(entity);
        entity.Validate(EnumSceneFlags.Update | EnumSceneFlags.ForceValidate);
        await _dac.UpdateAsync(entity, ct);
        OnAfterUpdate(entity);
    }

    internal virtual async Task<bool> InternalHardDeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        if (entity == null) return false;

        OnBeforeDelete(entity);
        var success = await _dac.DeleteAsync(entity, ct);
        if (success) OnAfterDelete(entity);
        return success;
    }

    #endregion
}