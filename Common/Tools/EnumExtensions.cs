using System;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 枚举类型的扩展方法。
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 获取枚举值的 DisplayName 特性值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">枚举值。</param>
    /// <returns>DisplayName。</returns>
    public static string GetEnumValueDisplay<T>(this T value) where T : struct, Enum
        => EnumHelper.GetEnumValueDisplayName(value);

    /// <summary>
    /// 获取枚举值对应的整数值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">枚举值。</param>
    /// <returns>整数值。</returns>
    public static int ToInt<T>(this T value) where T : struct, Enum
        => EnumHelper.GetIntValue(value);

    /// <summary>
    /// 检查枚举值是否为定义的值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">枚举值。</param>
    /// <returns>是否有效。</returns>
    public static bool IsValid<T>(this T value) where T : struct, Enum
        => EnumHelper.IsValidEnumValue(value);
}