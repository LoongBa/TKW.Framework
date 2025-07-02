using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 针对 Enum 的扩展方法，获取 DisplayAttribute。
/// </summary>
public static class EnumDisplayAttributeExtensions
{
    /// <summary>
    /// 获取枚举值的 DisplayAttribute，如果不存在则返回 null。
    /// </summary>
    /// <param name="enumValue">枚举值。</param>
    /// <returns>DisplayAttribute 实例或 null。</returns>
    public static DisplayAttribute? GetDisplayAttribute(this Enum enumValue)
    {
        ArgumentNullException.ThrowIfNull(enumValue, nameof(enumValue));
        var type = enumValue.GetType();
        var memberInfo = type.GetMember(enumValue.ToString());
        if (memberInfo.Length > 0)
        {
            return memberInfo[0].GetCustomAttribute<DisplayAttribute>(false);
        }
        return null;
    }
}