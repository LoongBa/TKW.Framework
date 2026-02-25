using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Domain.Interfaces;

public interface IDomainDto<TEntity> where TEntity : IDomainEntity
{
    /// <summary>
    /// 应用 DTO 的数据到指定的实体对象上，并返回更新后的实体对象。
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="scene">区分场景</param>
    TEntity ApplyToEntity(TEntity entity, EnumSceneFlags scene = EnumSceneFlags.Update);

    /// <summary>
    /// 执行 DTO 数据的业务预校验
    /// </summary>
    void ValidateData(EnumSceneFlags scene);

    /// <summary>
    /// 是否来自持久化来源（如数据库查询）。
    /// 如果为 true，表示 DTO 的数据是从数据库等持久化存储中查询得到的；
    /// 如果为 false，表示 DTO 是新创建的、未持久化的数据对象。
    /// 用于帮助业务逻辑区分处理新数据和已存在数据的不同场景。
    /// </summary>
    bool IsFromPersistentSource { get; }
}