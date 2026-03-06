using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

public static class MetadataExtensions
{
    /// <summary>
    /// DTO 验证器工厂：挂载到 IDomainDto 接口
    /// </summary>
    public static FluentValidator<TDto> CreateDtoValidator<TEntity, TDto>(
        this TDto dto,
        EnumSceneFlags scene,
        List<ValidationResult> results)
        where TDto : IDomainDto<TEntity>, ISupportPersistenceState
        where TEntity : IDomainEntity
    {
        // 内部自动关联静态泛型缓存
        return new FluentValidator<TDto>(dto, scene, ValidationModeEnum.Dto, results);
    }

    /// <summary>
    /// Model 验证器工厂：挂载到 IDomainEntity 接口
    /// </summary>
    public static FluentValidator<TEntity> CreateModelValidator<TEntity>(
        this TEntity entity,
        EnumSceneFlags scene,
        List<ValidationResult> results)
        where TEntity : IDomainEntity, ISupportPersistenceState
    {
        return new FluentValidator<TEntity>(entity, scene, ValidationModeEnum.Model, results);
    }

    /// <summary>
    /// 映射执行器：由于需要 Meta 字典参与决策，保留原有 Meta 扩展或改由 ValidationCache 提供
    /// </summary>
    public static MappingExecutor<TEntity, TDto> CreateMapper<TEntity, TDto>(
        this TDto dto,
        TEntity entity,
        EnumSceneFlags scene)
        where TDto : IDomainDto<TEntity>
        where TEntity : IDomainEntity
    {
        // 自动关联静态泛型缓存，消除生成代码中的 ValidationCache 显式引用
        var meta = ValidationCache<TDto>.Meta;
        return new MappingExecutor<TEntity, TDto>(dto, entity, scene, meta);
    }
}