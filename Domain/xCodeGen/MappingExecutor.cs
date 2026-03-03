using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

public class MappingExecutor<TEntity, TDto>(
    TDto dto,
    TEntity entity,
    EnumSceneFlags scene,
    IReadOnlyDictionary<string, PropertyMetadata> meta)
    where TDto : IDomainDto<TEntity>
    where TEntity : IDomainEntity
{
    private readonly TDto _Dto = dto;

    public MappingExecutor<TEntity, TDto> Map<TValue>(
        string propName,
        Func<TDto, TValue> dtoGetter,
        Action<TEntity, TValue> entitySetter,
        Func<TEntity, TValue>? entityGetter = null)
    {
        // 判定是否允许映射 (checkType: 0)
        if (!CodeGenPolicy.CanProcess(meta, propName, scene, _Dto.IsFromPersistentSource, 0))
            return this;

        var newValue = dtoGetter(_Dto);

        // 掩码写回防护：只有启用掩码且用户输入的是掩码字符串时，跳过回填
        if (newValue is string strNewValue && entityGetter != null && meta.TryGetValue(propName, out var pMeta))
        {
            var dtoAttr = pMeta.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
            if (CodeGenPolicy.GetBoolProp(dtoAttr, "Masking", false))
            {
                var pattern = CodeGenPolicy.GetStringProp(dtoAttr, "MaskPattern");
                var currentEntityValue = entityGetter(entity)?.ToString();

                // 使用迁移后的 MaskHelper 进行对比
                if (strNewValue == MaskHelper.GetMaskedValue(currentEntityValue, pattern))
                    return this;
            }
        }

        entitySetter(entity, newValue);
        return this;
    }
}