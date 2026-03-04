using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 映射执行器：负责将 DTO 数据安全地回填至实体，内含策略判定与掩码防护
/// </summary>
public class MappingExecutor<TEntity, TDto>(
    TDto dto,
    TEntity entity,
    EnumSceneFlags scene,
    IReadOnlyDictionary<string, PropertyMetadata> meta)
    where TDto : IDomainDto<TEntity>
    where TEntity : IDomainEntity
{
    private readonly TDto _Dto = dto;
    private readonly TEntity _Entity = entity;

    /// <summary>
    /// 执行属性映射
    /// </summary>
    /// <param name="propName">属性名称</param>
    /// <param name="dtoGetter">DTO 属性读取委托</param>
    /// <param name="entitySetter">实体属性设置委托</param>
    /// <param name="entityGetter">实体属性读取委托（用于掩码对比，可选）</param>
    public MappingExecutor<TEntity, TDto> Map<TValue>(
        string propName,
        Func<TDto, TValue> dtoGetter,
        Action<TEntity, TValue> entitySetter,
        Func<TEntity, TValue>? entityGetter = null)
    {
        // 1. 提取元数据并进行策略判定
        meta.TryGetValue(propName, out var pMeta);

        // 使用统一决策引擎判定是否允许回填
        if (!CodeGenPolicy.CanProcess(pMeta, scene, _Dto.IsFromPersistentSource, ValidationModeEnum.Mapping))
            return this;

        var newValue = dtoGetter(_Dto);

        // 2. 掩码写回防护：若输入值等于掩码处理后的旧值，判定为未编辑，跳过回填
        if (newValue is string strNewValue && entityGetter != null && pMeta != null)
        {
            var dtoAttr = pMeta.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
            if (CodeGenPolicy.GetBoolProp(dtoAttr, "Masking", false))
            {
                var pattern = CodeGenPolicy.GetStringProp(dtoAttr, "MaskPattern");
                var currentEntityValue = entityGetter(_Entity)?.ToString();

                // 核心防护：防止掩码字符被写入数据库
                if (strNewValue == MaskHelper.GetMaskedValue(currentEntityValue, pattern))
                {
                    return this;
                }
            }
        }

        // 3. 执行赋值
        entitySetter(_Entity, newValue);
        return this;
    }
}