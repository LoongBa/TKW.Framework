using System;
using System.Collections.Generic;
using System.Linq;
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
        // 1. 系统级：审计填充 (优先于钩子和校验)
        HandleAuditOnCreate(entity);

        // 2. 业务级：拦截钩子
        OnBeforeCreate(entity);

        // 3. 校验级：实体强验证
        entity.Validate(EnumSceneFlags.Create | EnumSceneFlags.ForceValidate);

        // 4. 持久级
        await Dac.InsertAsync(entity, ct);

        OnAfterCreate(entity);
        return entity;
    }
    protected internal virtual async Task<List<TEntity>> InternalCreateBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        // 1. 物化集合，消除多次枚举隐患
        var entityArray = entities as TEntity[] ?? entities.ToArray();
        if (entityArray.Length == 0) return new List<TEntity>();

        foreach (var entity in entityArray)
        {
            // 2. 依次执行系统审计、拦截钩子和实体校验
            HandleAuditOnCreate(entity);
            OnBeforeCreate(entity);
            entity.Validate(EnumSceneFlags.Create | EnumSceneFlags.ForceValidate);
        }

        // 3. 执行持久化
        await Dac.InsertBatchAsync(entityArray, ct);

        // 4. 执行创建后钩子 (此时 ID 已由底层 ORM 自动回填)
        foreach (var entity in entityArray)
        {
            OnAfterCreate(entity);
        }

        // 返回包含已回填 ID 的列表
        return entityArray.ToList();
    }

    protected internal virtual async Task InternalUpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        // 1. 系统级：审计填充
        HandleAuditOnUpdate(entity);

        OnBeforeUpdate(entity);
        entity.Validate(EnumSceneFlags.Update | EnumSceneFlags.ForceValidate);
        await Dac.UpdateAsync(entity, ct);
        OnAfterUpdate(entity);
    }

    protected internal virtual async Task InternalUpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        var entityArray = entities as TEntity[] ?? entities.ToArray();
        if (entityArray.Length == 0) return;

        foreach (var entity in entityArray)
        {
            // 批量操作同样需要触发审计和校验
            HandleAuditOnUpdate(entity);
            OnBeforeUpdate(entity);
            entity.Validate(EnumSceneFlags.Update | EnumSceneFlags.ForceValidate);
        }

        await Dac.UpdateBatchAsync(entityArray, ct);

        foreach (var entity in entityArray) OnAfterUpdate(entity);
    }

    protected internal virtual async Task<int> InternalUpdateColumnsBatchAsync(
        IEnumerable<TEntity> entities,
        System.Linq.Expressions.Expression<Func<TEntity, object>> columns,
        CancellationToken ct = default)
    {
        var entityArray = entities as TEntity[] ?? entities.ToArray();
        if (entityArray.Length == 0) return 0;

        foreach (var entity in entityArray)
        {
            // 即使是局部更新，内存中的对象也应当反映最新的审计状态
            HandleAuditOnUpdate(entity);
            OnBeforeUpdate(entity);
        }

        // 注意：底层 DAC 执行时，必须在 columns 表达式中显式包含 UpdatedTime 等审计列才能同步到数据库
        var rows = await Dac.UpdateColumnsBatchAsync(entityArray, columns, ct);

        foreach (var entity in entityArray)
        {
            OnAfterUpdate(entity);
        }

        return rows;
    }

    protected internal virtual async Task<bool> InternalHardDeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        if (entity == null) return false;

        OnBeforeDelete(entity);

        bool success;
        // 自动化处理软删除逻辑
        if (HasSoftDelete && entity is IEntitySoftDelete soft)
        {
            soft.IsDeleted = true;
            // 软删除本质上是一次特定的审计更新
            HandleAuditOnUpdate(entity);
            await Dac.UpdateAsync(entity, ct);
            success = true;
        }
        else
        {
            success = await Dac.DeleteAsync(entity, ct);
        }

        if (success) OnAfterDelete(entity);
        return success;
    }
    #endregion

    #region [ 私有审计助手 ]

    private void HandleAuditOnCreate(TEntity entity)
    {
        if (entity is IAuditEntity audit)
        {
            var now = DateTime.Now;
            var userName = User.UserInfo?.UserName ?? "System";
            audit.CreatedTime = now;
            audit.CreatedBy = userName;
            audit.UpdatedTime = now;
            audit.UpdatedBy = userName;
        }
    }

    private void HandleAuditOnUpdate(TEntity entity)
    {
        if (entity is IAuditEntity audit)
        {
            audit.UpdatedTime = DateTime.Now;
            audit.UpdatedBy = User.UserInfo?.UserName ?? "System";
        }
    }

    #endregion
}