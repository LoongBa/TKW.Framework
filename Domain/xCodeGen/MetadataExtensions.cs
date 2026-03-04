using System.Collections.Generic;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

public static class MetadataExtensions
{
    extension(IReadOnlyDictionary<string, PropertyMetadata> meta)
    {
        /// <summary>
        /// 初始化映射执行器
        /// </summary>
        public MappingExecutor<TEntity, TDto> CreateMapper<TDto, TEntity>(TDto dto, TEntity entity, EnumSceneFlags scene)
            where TDto : IDomainDto<TEntity>
            where TEntity : IDomainEntity
        {
            return new MappingExecutor<TEntity, TDto>(dto, entity, scene, meta);
        }
        /// <summary> DTO 验证器工厂 </summary>
        public FluentValidator<TDto> CreateDtoValidator<TEntity, TDto>(TDto dto, EnumSceneFlags scene)
            where TDto : IDomainDto<TEntity> where TEntity : IDomainEntity
            => new(dto, scene, meta, ValidationModeEnum.Dto);

        /// <summary> Model 验证器工厂 </summary>
        public FluentValidator<TEntity> CreateModelValidator<TEntity>(TEntity entity, EnumSceneFlags scene)
            where TEntity : IDomainEntity
            => new(entity, scene, meta, ValidationModeEnum.Model);
    }
}