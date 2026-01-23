using System;

namespace TKW.Framework.Domain.Interfaces;

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
    /// 仅创建场景必填（简写：CreateRequired=true 等价于 CreateOverride.IsRequired=true）
    /// </summary>
    public bool CreateRequired { get; set; }
    /// <summary>
    /// 仅更新场景不可改（简写：UpdateReadOnly=true 等价于 UpdateOverride.CanModify=false）
    /// </summary>
    public bool UpdateReadOnly { get; set; }
    /// <summary>
    /// 仅详情场景隐藏（简写：DetailsHidden=true 等价于 DetailsOverride.IsVisible=false）
    /// </summary>
    public bool DetailsHidden { get; set; }

    // 扩展：支持多场景组合（如Create/Update都必填）
    public SceneFlags RequiredScenes { get; set; } = SceneFlags.None;
    public SceneFlags ReadOnlyScenes { get; set; } = SceneFlags.None;
    public SceneFlags HiddenScenes { get; set; } = SceneFlags.None;
}