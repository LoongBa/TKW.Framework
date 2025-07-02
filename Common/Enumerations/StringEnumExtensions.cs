using System;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 字符串类型的枚举扩展方法。
/// </summary>
public static class StringEnumExtensions
{
    /// <summary>
    /// 将字符串解析为枚举值（基于 DisplayName 特性）。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">字符串。</param>
    /// <returns>枚举值。</returns>
    public static T ToEnumByDisplay<T>(this string value) where T : struct, Enum
        => EnumHelper.ParseEnumValueByDisplay<T>(value);

    /// <summary>
    /// 将字符串解析为枚举值（基于枚举名称）。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">字符串。</param>
    /// <returns>枚举值。</returns>
    public static T ToEnum<T>(this string value) where T : struct, Enum
        => EnumHelper.Parse<T>(value);

    /// <summary>
    /// 将字符串解析为枚举值（基于枚举名称，失败时返回默认值）。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">字符串。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>枚举值。</returns>
    public static T ToEnum<T>(this string value, T defaultValue) where T : struct, Enum
        => EnumHelper.Parse(value, defaultValue);

    /// <summary>
    /// 检查字符串是否匹配枚举的名字。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">字符串。</param>
    /// <returns>是否有效。</returns>
    public static bool IsValidEnumName<T>(this string value) where T : struct, Enum
        => EnumHelper.IsValidEnumName<T>(value);

    /// <summary>
    /// 检查字符串是否匹配枚举的 DisplayName。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="value">字符串。</param>
    /// <returns>是否有效。</returns>
    public static bool IsValidEnumDisplay<T>(this string value) where T : struct, Enum
        => EnumHelper.IsValidEnumDisplay<T>(value);
}