using System;
using System.Collections.Generic;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 泛型类型的枚举扩展方法。
/// </summary>
public static class GenericEnumExtensions
{
    /// <summary>
    /// 获取枚举类型的所有 DisplayName。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumType">枚举类型实例（未使用，仅用于泛型约束）。</param>
    /// <returns>DisplayName 列表。</returns>
    public static List<string> GetDisplayNames<T>(this T enumType) where T : struct, Enum
        => EnumHelper.GetEnumDisplayNames<T>();

    /// <summary>
    /// 获取枚举类型的所有值与 DisplayName 的映射。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumType">枚举类型实例（未使用，仅用于泛型约束）。</param>
    /// <returns>值与 DisplayName 的映射。</returns>
    public static Dictionary<T, string> GetValueDisplayMap<T>(this T enumType) where T : struct, Enum
        => EnumHelper.GetEnumValueDisplayMap<T>();

    /// <summary>
    /// 根据 DisplayName 获取对应的枚举值集合。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumType">枚举类型实例（未使用，仅用于泛型约束）。</param>
    /// <param name="displayName">DisplayName。</param>
    /// <returns>枚举值集合。</returns>
    public static List<T> GetValuesByDisplay<T>(this T enumType, string displayName) where T : struct, Enum
        => EnumHelper.GetEnumValuesByDisplay<T>(displayName);

    /// <summary>
    /// 获取随机枚举值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumType">枚举类型实例（未使用，仅用于泛型约束）。</param>
    /// <returns>随机枚举值。</returns>
    public static T GetRandomValue<T>(this T enumType) where T : struct, Enum
        => EnumHelper.GetRandomEnumValue<T>();
}