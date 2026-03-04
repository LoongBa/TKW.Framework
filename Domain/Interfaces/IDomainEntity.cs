using xCodeGen.Abstractions;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// Domain 实体标记接口
/// </summary>
public interface IDomainEntity: ISupportPersistenceState
{
    /// <summary> 执行业务预校验，返回所有校验结果 </summary>
    void Validate(EnumSceneFlags scene);
}