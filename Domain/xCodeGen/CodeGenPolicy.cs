using System;
using System.Linq;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 核心决策引擎：统一管理属性的回填、验证策略 (V1.25 - 迁移至领域层)
/// </summary>
public static class CodeGenPolicy
{
    private const string DtoFieldAttr = "DtoFieldAttribute";
    private const string ColumnAttr = "ColumnAttribute";

    #region 核心决策逻辑

    /// <summary>
    /// 统一判定函数：判定当前属性在给定场景和模式下是否允许执行
    /// </summary>
    /// <param name="pMeta">属性元数据</param>
    /// <param name="scene">业务场景（创建/更新/强制校验等）</param>
    /// <param name="isFromPersistent">是否来自持久化源</param>
    /// <param name="mode">判定模式（映射/DTO/Model）</param>
    public static bool CanProcess(PropertyMetadata? pMeta, EnumSceneFlags scene, bool isFromPersistent, ValidationModeEnum mode)
    {
        if (pMeta == null) return false;

        return mode switch
        {
            ValidationModeEnum.Dto => ShouldValidateDto(pMeta, scene, isFromPersistent),
            ValidationModeEnum.Model => ShouldValidateModel(pMeta, scene, isFromPersistent),
            ValidationModeEnum.Mapping => ShouldMapToEntity(pMeta, scene),
            _ => false
        };
    }

    private static bool IsAutoManaged(PropertyMetadata p)
    {
        var autoFields = new[] { "Id", "CreateTime", "UpdateTime", "IsDeleted", "TenantId" };
        if (autoFields.Contains(p.Name)) return true;

        var col = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains(ColumnAttr));
        return col != null && (GetBoolProp(col, "IsIdentity", false) || GetBoolProp(col, "IsPrimary", false));
    }

    private static bool ShouldMapToEntity(PropertyMetadata p, EnumSceneFlags scene)
    {
        if (IsAutoManaged(p)) return false;
        var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains(DtoFieldAttr));
        if (dtoAttr == null) return true;

        if (GetBoolProp(dtoAttr, "CanModify", true) == false) return false;
        if (scene == EnumSceneFlags.Update && GetBoolProp(dtoAttr, "UpdateReadOnly", false)) return false;

        return true;
    }

    private static bool ShouldValidateDto(PropertyMetadata p, EnumSceneFlags scene, bool isFromPersistent)
    {
        // 策略一：持久化信任 (Persistence Trust)
        if (isFromPersistent && (scene & EnumSceneFlags.ForceValidate) == 0) return false;
        return ShouldMapToEntity(p, scene);
    }

    private static bool ShouldValidateModel(PropertyMetadata p, EnumSceneFlags scene, bool isFromPersistent)
    {
        // 策略三：模型终验制 (Model Final Validation)
        if ((scene & EnumSceneFlags.ForceValidate) != 0) return true;
        return !isFromPersistent;
    }

    #endregion

    #region 元数据安全解析辅助

    public static bool GetBoolProp(AttributeMetadata attr, string key, bool defaultValue)
    {
        if (attr != null && attr.Properties.TryGetValue(key, out var val))
            return val is bool b ? b : (val?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
        return defaultValue;
    }

    public static string GetStringProp(AttributeMetadata attr, string key) =>
        attr != null && attr.Properties.TryGetValue(key, out var val) ? val?.ToString() : string.Empty;

    #endregion
}