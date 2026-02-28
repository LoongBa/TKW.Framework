using xCodeGen.Abstractions;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// Domain 实体标记接口
/// </summary>
public interface IDomainEntity
{
    /// <summary> 执行业务预校验，返回所有校验结果 </summary>
    void Validate(EnumSceneFlags scene);
}

public static class DomainEntityExtensions
{
    /// <summary>
    /// 更新场景的验证
    /// </summary>
    public static void CreateValidate(this IDomainEntity entity) 
        => entity.Validate(EnumSceneFlags.Create);
    /// <summary>
    /// 更新场景的验证
    /// </summary>
    public static void UpdateValidate(this IDomainEntity entity)
        => entity.Validate(EnumSceneFlags.Update);

    /// <summary>
    /// 获取信息场景的验证
    /// </summary>
    public static void DetailsValidate(this IDomainEntity entity)
        => entity.Validate(EnumSceneFlags.Details);
}