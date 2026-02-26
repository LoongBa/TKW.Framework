using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Domain.Interfaces;

public interface IDomainDto<TEntity> where TEntity : IDomainEntity
{
    /// <summary> 应用 DTO 数据到实体 </summary>
    TEntity ApplyToEntity(TEntity entity, EnumSceneFlags scene = EnumSceneFlags.Update);

    /// <summary> 执行业务预校验，返回所有校验结果 </summary>
    IEnumerable<ValidationResult> ValidateData(EnumSceneFlags scene);

    /// <summary>
    /// 是否来自持久化来源（如数据库查询）。
    /// 如果为 true，表示 DTO 的数据是从数据库等持久化存储中查询得到的；
    /// 如果为 false，表示 DTO 是新创建的、未持久化的数据对象。
    /// 用于帮助业务逻辑区分处理新数据和已存在数据的不同场景。
    /// </summary>
    bool IsFromPersistentSource { get; }
}