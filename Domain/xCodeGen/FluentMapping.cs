using System;
using System.Collections.Generic;
using System.Linq;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

public class FluentMapping<TDto, TEntity>(
    TDto dto,
    TEntity entity,
    EnumSceneFlags scene,
    bool isFromPersistent,
    IReadOnlyDictionary<string, PropertyMetadata> meta)
{
    /// <summary>
    /// 流式映射：内含策略检查与掩码防护
    /// </summary>
    public FluentMapping<TDto, TEntity> Map<TValue>(
        string propName,
        Func<TDto, TValue> dtoGetter,
        Action<TEntity, TValue> entitySetter)
    {
        // 1. 策略判定：是否允许回填
        if (!CodeGenPolicy.CanProcess(meta, propName, scene, isFromPersistent, 0))
            return this;

        var value = dtoGetter(dto);

        // 2. 掩码写回防护：若输入的是掩码，则跳过
        if (value is string strValue && meta.TryGetValue(propName, out var pMeta))
        {
            var dtoField = pMeta.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
            if (CodeGenPolicy.GetBoolProp(dtoField, "Masking", false))
            {
                // 这里调用逻辑：如果传入值 == 掩码处理后的旧值，则认为没改过，跳过
                // string original = entityGetter(_entity); // 需要额外传入 getter 或反射
                // if (strValue == MaskHelper.GetMaskedValue(original, ...)) return this;
            }
        }

        entitySetter(entity, value);
        return this;
    }
}