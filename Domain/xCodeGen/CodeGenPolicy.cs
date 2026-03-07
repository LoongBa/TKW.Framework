using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 核心决策引擎：统一管理属性的回填、验证及 Service 查询生成策略 (V1.41)
/// </summary>
public static class CodeGenPolicy
{
    private const string DtoFieldAttr = "DtoFieldAttribute";
    private const string ColumnAttr = "ColumnAttribute";
    private const string IndexAttr = "IndexAttribute";

    public const int DefaultPageSize = 20;
    public const int DefaultLimit = 1000;
    public const int MaxSearchLimit = 500;

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
        public string GroupName { get; set; } = string.Empty;
        public List<PropertyMetadata> Properties { get; set; } = [];
        public bool IsUnique { get; set; }
    }

    public static List<SearchGroupInfo> GetSearchGroups(ClassMetadata classMeta)
    {
        var groups = new List<SearchGroupInfo>();
        var props = classMeta.Properties.ToList();
        var className = classMeta.ClassName;

        var searchGroupMap = new Dictionary<string, List<PropertyMetadata>>();

        // 1. 处理 DtoFieldAttribute (最高优先级)
        foreach (var p in props)
        {
            var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains(DtoFieldAttr));
            if (dtoAttr == null) continue;

            var isUnique = GetBoolProp(dtoAttr, "IsUnique", false);
            var isSearchable = GetBoolProp(dtoAttr, "IsSearchable", false);
            var groupName = GetStringProp(dtoAttr, "SearchGroup");

            // 1.1 独立查询 (IsSearchable 或 IsUnique)
            if (isSearchable || isUnique)
            {
                groups.Add(new SearchGroupInfo
                {
                    GroupName = ToPascalCase(p.Name, className),
                    Properties = [p],
                    IsUnique = isUnique
                });
            }

            // 1.2 复合查询 (SearchGroup)
            if (!string.IsNullOrEmpty(groupName))
            {
                if (!searchGroupMap.ContainsKey(groupName)) searchGroupMap[groupName] = [];
                searchGroupMap[groupName].Add(p);
            }
        }

        foreach (var kvp in searchGroupMap)
        {
            var pName = ToPascalCase(kvp.Key, className);
            if (kvp.Value.Count == 1 && groups.Any(g => g.GroupName == pName)) continue;

            groups.Add(new SearchGroupInfo
            {
                GroupName = pName,
                Properties = kvp.Value,
                IsUnique = GetBoolProp(kvp.Value[0].Attributes.FirstOrDefault(a => a.TypeFullName.Contains(DtoFieldAttr)), "IsUnique", false)
            });
        }

        // 2. 处理 IndexAttribute (低优先级，自动避让已处理的同名字段组合)
        var indices = classMeta.Attributes.Where(a => a.TypeFullName.Contains(IndexAttr)).ToList();
        foreach (var idx in indices)
        {
            var fieldsStr = idx.ConstructorArguments.Count > 1 ? idx.ConstructorArguments.ElementAt(1)?.ToString() : "";
            if (string.IsNullOrEmpty(fieldsStr)) continue;

            var columnNames = fieldsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
            var groupProps = props.Where(p => columnNames.Contains(p.Name)).ToList();
            if (!groupProps.Any()) continue;

            // 核心排他逻辑：如果 DtoField 已经处理过完全相同的字段组合，则忽略此索引
            if (groups.Any(g => g.Properties.Select(p => p.Name).OrderBy(n => n).SequenceEqual(columnNames.OrderBy(n => n)))) continue;

            var businessName = groupProps.Count == 1 ? groupProps[0].Name : ExtractIndexName(idx.ConstructorArguments.ElementAtOrDefault(0)?.ToString(), className, groupProps);

            groups.Add(new SearchGroupInfo
            {
                GroupName = ToPascalCase(businessName, className),
                Properties = groupProps,
                IsUnique = GetBoolProp(idx, "IsUnique", false)
            });
        }

        return groups.OrderBy(g => g.GroupName).ToList();
    }

    private static string ExtractIndexName(string? indexName, string className, List<PropertyMetadata> props)
    {
        if (string.IsNullOrWhiteSpace(indexName)) return string.Join("And", props.Select(p => p.Name));

        var parts = indexName.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
        var pascalName = string.Concat(parts.Select(p => char.ToUpper(p[0]) + p[1..]));

        if (pascalName.StartsWith("Idx", StringComparison.OrdinalIgnoreCase)) pascalName = pascalName[3..];
        else if (pascalName.StartsWith("Index", StringComparison.OrdinalIgnoreCase)) pascalName = pascalName[5..];

        if (pascalName.StartsWith(className, StringComparison.OrdinalIgnoreCase)) pascalName = pascalName[className.Length..];

        if (string.IsNullOrWhiteSpace(pascalName) || pascalName.Length < 2)
            return string.Join("And", props.Select(p => p.Name));

        return pascalName;
    }

    private static string ToPascalCase(string input, string? className = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Query";
        var clean = Regex.Replace(input, @"[^a-zA-Z0-9_]", "");

        if (!string.IsNullOrEmpty(className) && clean.StartsWith(className, StringComparison.OrdinalIgnoreCase))
            clean = clean[className.Length..];

        var parts = clean.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
        var result = string.Concat(parts.Select(p => char.ToUpper(p[0]) + p[1..]));

        return string.IsNullOrEmpty(result) ? "Group" : result;
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