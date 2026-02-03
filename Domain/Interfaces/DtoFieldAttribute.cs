using System;

namespace TKW.Framework.Domain.Interfaces;
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
    // 统一规则（默认值）
    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; } = false;
    public bool CanModify { get; set; } = true;

    // 差异化配置：用“场景+规则”的简写参数，无需new对象
    /// <summary>
    /// 仅创建场景必填（简写：CreateRequired=true 等价于 IsRequired=true）
    /// </summary>
    public bool CreateRequired { get; set; }
    /// <summary>
    /// 仅更新场景不可改（简写：UpdateReadOnly=true 等价于 CanModify=false）
    /// </summary>
    public bool UpdateReadOnly { get; set; }
    /// <summary>
    /// 仅详情场景隐藏（简写：DetailsHidden=true 等价于 IsVisible=false）
    /// </summary>
    public bool DetailsHidden { get; set; }

    // 扩展：支持多场景组合（如Create/Update都必填）
    public EnumSceneFlags RequiredScenes { get; set; } = EnumSceneFlags.None;
    public EnumSceneFlags ReadOnlyScenes { get; set; } = EnumSceneFlags.None;
    public EnumSceneFlags HiddenScenes { get; set; } = EnumSceneFlags.None;
}