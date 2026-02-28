using System;
using System.Collections.Generic;
using System.Linq;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Abstractions
{
    /// <summary>
    /// 核心决策引擎：统一管理属性的回填、验证及查询生成策略 (V1.23)
    /// </summary>
    public static class CodeGenPolicy
    {
        private const string DtoFieldAttr = "DtoFieldAttribute";
        private const string ColumnAttr = "ColumnAttribute";

        #region 映射与验证策略

        public static bool IsAutoManaged(PropertyMetadata p)
        {
            var autoFields = new[] { "Id", "CreateTime", "UpdateTime", "IsDeleted", "TenantId" };
            if (autoFields.Contains(p.Name)) return true;

            var col = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("ColumnAttribute"));
            return col != null && (GetBoolProp(col, "IsIdentity", false) || GetBoolProp(col, "IsPrimary", false));
        }

        public static bool ShouldMapToEntity(PropertyMetadata p, EnumSceneFlags scene)
        {
            if (IsAutoManaged(p)) return false;
            var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains(DtoFieldAttr));
            if (dtoAttr == null) return true;
            if (GetBoolProp(dtoAttr, "CanModify", true) == false) return false;
            if (scene == EnumSceneFlags.Update && GetBoolProp(dtoAttr, "UpdateReadOnly", false)) return false;
            return true;
        }

        public static bool ShouldValidateDto(PropertyMetadata p, EnumSceneFlags scene, bool isFromPersistent)
        {
            if (isFromPersistent && (scene & EnumSceneFlags.ForceValidate) == 0) return false;
            return ShouldMapToEntity(p, scene);
        }

        public static bool ShouldValidateModel(PropertyMetadata p, EnumSceneFlags scene, bool isFromPersistent)
        {
            if ((scene & EnumSceneFlags.ForceValidate) != 0) return true;
            return !isFromPersistent;
        }

        /// <summary>
        /// 统一判定函数：合并字典查找与策略判定
        /// </summary>
        /// <param name="checkType">0-回填(Map), 1-DTO校验, 2-Model校验</param>
        /// <param name="map"></param>
        public static bool CanProcess(IReadOnlyDictionary<string, PropertyMetadata> map, string propertyName, EnumSceneFlags scene, bool isFromPersistent, int checkType)
        {
            if (!map.TryGetValue(propertyName, out var pMeta)) return false;
            switch (checkType)
            {
                case 0:
                    return ShouldMapToEntity(pMeta, scene);
                case 1:
                    return ShouldValidateDto(pMeta, scene, isFromPersistent);
                case 2:
                    return ShouldValidateModel(pMeta, scene, isFromPersistent);
                default:
                    return false;
            }
        }

        #endregion

        #region 安全解析辅助方法

        public static bool GetBoolProp(AttributeMetadata attr, string key, bool defaultValue)
        {
            if (attr != null && attr.Properties.TryGetValue(key, out var val))
                return val is bool b ? b : (val?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
            return defaultValue;
        }

        public static string GetStringProp(AttributeMetadata attr, string key) =>
            attr != null && attr.Properties.TryGetValue(key, out var val) ? val?.ToString() : null;

        public static int GetIntProp(AttributeMetadata attr, string key, int defaultValue)
        {
            if (attr != null && attr.Properties.TryGetValue(key, out var val) && val != null)
            {
                try { return Convert.ToInt32(val); } catch { return defaultValue; }
            }
            return defaultValue;
        }

        #endregion
    }
}