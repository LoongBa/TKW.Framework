using System;
using System.Collections.Generic;
using System.Linq;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 核心决策引擎：统一管理属性的回填、验证及查询生成策略 (V1.30)
/// </summary>
public static class CodeGenPolicy
{
    private const string DtoFieldAttr = "DtoFieldAttribute";
    private const string ColumnAttr = "ColumnAttribute";
    private const string IndexAttr = "IndexAttribute";

    #region 核心决策逻辑 (Mapping & Validation)

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

    public static bool IsAutoManaged(PropertyMetadata p)
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
        if (isFromPersistent && (scene & EnumSceneFlags.ForceValidate) == 0) return false;
        return ShouldMapToEntity(p, scene);
    }

    private static bool ShouldValidateModel(PropertyMetadata p, EnumSceneFlags scene, bool isFromPersistent)
    {
        if ((scene & EnumSceneFlags.ForceValidate) != 0) return true;
        return !isFromPersistent;
    }

    #endregion

    #region Service 查询方法提取逻辑

    public class SearchGroupInfo
    {
        /// <summary> 方法名后缀（如 Code, GradeAndClass） </summary>
        public string GroupName { get; set; } = string.Empty;
        /// <summary> 参与查询的属性集合 </summary>
        public List<PropertyMetadata> Properties { get; set; } = new();
        /// <summary> 是否为唯一约束查询（决定生成 GetBy 还是 First/Select） </summary>
        public bool IsUnique { get; set; }
    }

    /// <summary>
    /// 提取类元数据中所有需要自动生成的查询组 (对齐文档 9.3)
    /// </summary>
    public static List<SearchGroupInfo> GetSearchGroups(ClassMetadata classMeta)
    {
        var groups = new List<SearchGroupInfo>();
        var props = classMeta.Properties.ToList();

        // 1. 处理 IndexAttribute (IsUnique = true)
        var indices = classMeta.Attributes.Where(a => a.TypeFullName.Contains(IndexAttr)).ToList();
        foreach (var idx in indices)
        {
            if (!GetBoolProp(idx, "IsUnique", false)) continue;

            // 修正 CS0021 错误：使用 ElementAt 访问 ICollection 元素，并访问 .Value
            // IndexAttribute(string name, string fields, bool isUnique) -> fields 是第 2 个参数 (index 1)
            var columnNames = idx.ConstructorArguments.Count > 1
                ? idx.ConstructorArguments.ElementAt(1)?.ToString()?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : null;

            if (columnNames == null) continue;

            var groupProps = props.Where(p => columnNames.Any(c => c.Trim().Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            if (groupProps.Any())
            {
                groups.Add(new SearchGroupInfo
                {
                    GroupName = string.Join("And", groupProps.Select(p => p.Name)),
                    Properties = groupProps,
                    IsUnique = true
                });
            }
        }

        // 2. 处理 DtoFieldAttribute 配置
        var searchGroupMap = new Dictionary<string, List<PropertyMetadata>>();

        foreach (var p in props)
        {
            var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains(DtoFieldAttr));
            if (dtoAttr == null) continue;

            // 2.1 独立唯一键 (IsUnique = true) -> 生成 GetBy...
            if (GetBoolProp(dtoAttr, "IsUnique", false))
            {
                // 去重：检查是否已被 Index 覆盖
                if (!groups.Any(g => g.Properties.Count == 1 && g.Properties[0].Name == p.Name))
                {
                    groups.Add(new SearchGroupInfo { GroupName = p.Name, Properties = new List<PropertyMetadata> { p }, IsUnique = true });
                }
            }

            // 2.2 SearchGroup 逻辑分组 -> 生成 First/Select...
            var groupName = GetStringProp(dtoAttr, "SearchGroup");
            if (!string.IsNullOrEmpty(groupName))
            {
                if (!searchGroupMap.ContainsKey(groupName)) searchGroupMap[groupName] = new List<PropertyMetadata>();
                searchGroupMap[groupName].Add(p);
            }
        }

        // 3. 将 SearchGroup 映射为 SearchGroupInfo
        foreach (var kvp in searchGroupMap)
        {
            groups.Add(new SearchGroupInfo
            {
                GroupName = kvp.Key,
                Properties = kvp.Value,
                IsUnique = false // SearchGroup 统一视为非唯一组合查询
            });
        }

        return groups.OrderBy(g => g.GroupName).ToList();
    }

    #endregion

    #region 元数据安全解析辅助

    public static bool GetBoolProp(AttributeMetadata? attr, string key, bool defaultValue)
    {
        if (attr != null && attr.Properties.TryGetValue(key, out var val))
            return val is bool b ? b : (val?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
        return defaultValue;
    }

    public static string GetStringProp(AttributeMetadata? attr, string key) =>
        attr != null && attr.Properties.TryGetValue(key, out var val) ? val?.ToString() ?? string.Empty : string.Empty;

    #endregion
}