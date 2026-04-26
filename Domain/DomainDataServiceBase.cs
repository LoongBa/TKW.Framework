using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域数据服务读写基类（适用于普通实体）
/// </summary>
public abstract class DomainDataServiceBase<TUserInfo, TEntity, TDto>(
    DomainUser<TUserInfo> user,
    IEntityDAC<TEntity> dac,
    bool hasSoftDelete = false,
    bool hasEnableStatus = false)
    : DomainReadOnlyDataServiceBase<TUserInfo, TEntity, TDto>(user, dac, hasSoftDelete, hasEnableStatus)
    where TUserInfo : class, IUserInfo, new()
    where TEntity : class, IDomainEntity, new()
    where TDto : class, IDomainDto<TEntity>
{
    #region [ 写入拦截钩子 - 虚方法 ]
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

    #region [ 内部原子逻辑 (写入) ]
    protected internal virtual async Task<TEntity> InternalCreateAsync(TEntity entity, CancellationToken ct = default)
    {
        OnBeforeCreate(entity);
        entity.Validate(EnumSceneFlags.Create | EnumSceneFlags.ForceValidate);
        await Dac.InsertAsync(entity, ct);
        OnAfterCreate(entity);
        return entity;
    }

    protected internal virtual async Task InternalUpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        OnBeforeUpdate(entity);
        entity.Validate(EnumSceneFlags.Update | EnumSceneFlags.ForceValidate);
        await Dac.UpdateAsync(entity, ct);
        OnAfterUpdate(entity);
    }

    protected internal virtual async Task<bool> InternalHardDeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        if (entity == null) return false;

        OnBeforeDelete(entity);
        var success = await Dac.DeleteAsync(entity, ct);
        if (success) OnAfterDelete(entity);
        return success;
    }
    #endregion
}