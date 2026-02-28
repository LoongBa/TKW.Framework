using System;

namespace xCodeGen.Abstractions
{
    /*
// 标注示例（优化后，配置量减少50%）
[DtoField(CreateRequired = true, DetailsHidden = true)] // 替代复杂的new DtoSceneOverride
public string AuditRemark { get; set; } = string.Empty;

// 组合场景示例
[DtoField(RequiredScenes = SceneFlags.Create | SceneFlags.Update)] // 创建+更新都必填
public string StoreName { get; set; } = string.Empty;
 */
    /// <summary>
    /// DTO 字段特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DtoFieldAttribute : Attribute
    {
        /// <summary> 是否忽略：若为 true 则 DTO 中忽略此字段</summary>
        public bool Ignore { get; set; } = false;
        /// <summary>
        /// 是否能修改：默认除主键外都可修改(ReadWrite)
        /// 否则可指定不可更改，如 UId 等
        /// 特殊情况（如某些业务字段） 需要在创建时可修改，更新时不可修改，
        /// 可以配合 UpdateReadOnly 场景化配置使用，如创建时间等
        /// </summary>
        public bool CanModify { get; set; } = true;
        /// <summary>
        /// 是否唯一键
        /// 唯一键在创建/更新时必填，是否只读取决于 CanModify
        /// 自动为唯一键生成查询方法（如 GetByCode）。
        /// </summary>
        public bool IsUnique { get; set; } = false;
        /// <summary>
        /// 是否支持查询：用于自动生成查询方法（如 GetByShortName）
        /// </summary>
        public bool IsSearchable { get; set; } = false;
        /// <summary>
        /// 关联查询分组（组名，用于方法名）
        /// 自动为同一组的字段生成查询方法（单一，或组合条件查询）
        /// 如果没有指定 SearchGroup，则 IsSearchable = true 的字段默认按字段名生成查询方法（如 GetByShortName）
        /// 如组名为 "Code" 的一个字段，生成查询方法 GetByCode(string code)
        /// 如同为组名为 "ClassAndName" 的两个字段，生成查询方法 GetByClassAndName(string class, string name)
        /// </summary>
        public string SearchGroup { get; set; } = string.Empty;
        /// <summary>
        /// 仅更新场景不可改 (UpdateReadOnly)
        /// （CanModify == true 的情况下，更新时不可改，回填与验证仅在 Create 场景生效）
        /// </summary>
        public bool UpdateReadOnly { get; set; }
        /// <summary>
        /// 友好的显示名称：用于校验错误消息的字段替换
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>
        /// 是否启用掩码脱敏 (Masking)
        /// </summary>
        public bool Masking { get; set; } = false;
        /// <summary>
        /// 掩码模式 (MaskPattern)
        /// ?: 原字符, #: 遮蔽符, ?*: 贪婪保留, #*: 贪婪遮蔽
        /// </summary>
        public string MaskPattern { get; set; } = string.Empty;
    }
}