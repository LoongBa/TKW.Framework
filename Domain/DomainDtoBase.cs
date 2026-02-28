using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域 DTO 基类：提供持久化状态标记及接口契约的默认存储
/// </summary>
/// <typeparam name="TEntity">关联的领域实体类型</typeparam>
public abstract record DomainDtoBase<TEntity> : IDomainDto<TEntity>
    where TEntity : IDomainEntity
{
    /// <summary>
    /// 是否来自持久化来源（如数据库查询）
    /// 用于执行“策略一：持久化信任”，若为 true 则跳过物理校验 [cite: 18]
    /// </summary>
    public bool IsFromPersistentSource { get; init; } = false;

    /// <summary>
    /// 应用 DTO 数据到实体 (由生成的代码实现)
    /// 内部应遵循“策略二：回填驱动验证”，仅回填可修改字段 [cite: 13, 14, 15]
    /// </summary>
    public abstract TEntity ApplyToEntity(TEntity entity, EnumSceneFlags scene = EnumSceneFlags.Update);

    /// <summary>
    /// 执行业务预校验 (由生成的代码实现)
    /// 内部应调用 ValidateDataCore 并聚合 OnCustomValidate 结果 [cite: 18, 19, 20, 21]
    /// </summary>
    public abstract void ValidateData(EnumSceneFlags scene);
}