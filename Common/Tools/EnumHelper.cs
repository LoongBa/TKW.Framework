using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 枚举工具类，提供基于 DisplayName 的枚举解析、映射、校验等功能。
/// </summary>
public static class EnumHelper
{
    // 使用 ConcurrentDictionary 确保线程安全
    private static readonly ConcurrentDictionary<Type, Dictionary<Enum, string>> EnumValueDisplayCache = new();
    private static readonly ConcurrentDictionary<Type, List<string>> AllDisplayNamesCache = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, List<Enum>>> DisplayNameToEnumValuesCache = new();
    private static readonly Random RandomInstance = new();

    /// <summary>
    /// 根据枚举类型的 DisplayName 返回对应的枚举值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValueString">枚举值的 DisplayName。</param>
    /// <returns>与 DisplayName 对应的枚举值。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="enumValueString"/> 为 null 或空白字符串时抛出。</exception>
    /// <exception cref="NotSupportedException">当指定的 DisplayName 没有对应的枚举值时抛出。</exception>
    public static T ParseEnumValueByDisplay<T>(string enumValueString) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enumValueString, nameof(enumValueString));

        var displayNameToEnumValues = GetDisplayToEnumValuesMap(typeof(T));
        if (displayNameToEnumValues.TryGetValue(enumValueString, out var enumValues) && enumValues.Count > 0)
        {
            return (T)enumValues[0];
        }

        throw new NotSupportedException($"{typeof(T).Name} 尚不支持枚举值：'{enumValueString}'");
    }

    /// <summary>
    /// 将字符串转换成指定枚举类型的值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValueString">要转换的字符串。</param>
    /// <returns>转换后的枚举值。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="enumValueString"/> 无法转换为枚举值时抛出。</exception>
    public static T Parse<T>(string enumValueString) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enumValueString, nameof(enumValueString));
        if (Enum.TryParse(enumValueString, out T result))
        {
            return result;
        }
        throw new ArgumentException($"The string '{enumValueString}' cannot be parsed to enum type {typeof(T).Name}.");
    }

    /// <summary>
    /// 将字符串转换成指定枚举类型的值，如转换不成功，返回指定的默认值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValueString">要转换的字符串。</param>
    /// <param name="defaultValue">转换失败时返回的默认值。</param>
    /// <returns>转换后的枚举值，如果转换失败则返回 <paramref name="defaultValue"/>。</returns>
    public static T Parse<T>(string enumValueString, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(enumValueString))
            return defaultValue;
        return Enum.TryParse(enumValueString, out T result) ? result : defaultValue;
    }

    /// <summary>
    /// 获取枚举所有值对应的 DisplayName 列表。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <returns>包含枚举所有值对应 DisplayName 的列表。</returns>
    public static List<string> GetEnumDisplayNames<T>() where T : struct, Enum
    {
        return AllDisplayNamesCache.GetOrAdd(typeof(T), t =>
        {
            var names = new List<string>();
            var values = Enum.GetValues(t);
            foreach (var value in values)
            {
                try
                {
                    var displayName = ((Enum)value).GetDisplayAttribute()?.Name;
                    if (displayName != null)
                    {
                        names.Add(displayName);
                    }
                }
                catch (Exception ex)
                {
                    // 如需日志请替换此处
                    // Log.Error($"Error getting display name for enum value: {ex.Message}");
                }
            }
            return names;
        });
    }

    /// <summary>
    /// 获取枚举值的 DisplayName。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValue">枚举值。</param>
    /// <returns>DisplayName 或枚举名称。</returns>
    public static string GetEnumValueDisplayName<T>(T enumValue) where T : struct, Enum
    {
        return GetEnumValueDisplayMap<T>().TryGetValue(enumValue, out var displayName) ? displayName : enumValue.ToString();
    }

    /// <summary>
    /// 获取枚举值与 DisplayName 的映射字典。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <returns>枚举值与 DisplayName 的映射。</returns>
    public static Dictionary<T, string> GetEnumValueDisplayMap<T>() where T : struct, Enum
    {
        return EnumValueDisplayCache.GetOrAdd(typeof(T), t =>
        {
            var dictionary = new Dictionary<T, string>();
            var values = Enum.GetValues(t);
            foreach (var value in values)
            {
                try
                {
                    var enumValue = (T)value;
                    var displayName = enumValue.GetDisplayAttribute()?.Name;
                    if (displayName != null)
                    {
                        dictionary[enumValue] = displayName;
                    }
                }
                catch (Exception ex)
                {
                    // 如需日志请替换此处
                    // Log.Error($"Error getting display name for enum value: {ex.Message}");
                }
            }
            // 直接返回 dictionary，避免多余的 ToDictionary
            return dictionary.ToDictionary(kv => (Enum)kv.Key, kv => kv.Value);
        }).ToDictionary(kv => (T)kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// 根据 DisplayName 获取枚举值集合。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="displayName">DisplayName。</param>
    /// <returns>枚举值集合。</returns>
    public static List<T> GetEnumValuesByDisplay<T>(string displayName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return [];
        }

        var displayNameToEnumValues = GetDisplayToEnumValuesMap(typeof(T));
        if (displayNameToEnumValues.TryGetValue(displayName, out var enumValues))
        {
            return enumValues.Cast<T>().ToList();
        }

        return [];
    }

    /// <summary>
    /// 获取枚举值对应的整数值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValue">枚举值。</param>
    /// <returns>整数值。</returns>
    public static int GetIntValue<T>(T enumValue) where T : struct, Enum
        => Convert.ToInt32(enumValue);

    /// <summary>
    /// 根据整数值获取枚举值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="intValue">整数值。</param>
    /// <returns>枚举值。</returns>
    /// <exception cref="NotSupportedException">当指定的整数值没有对应的枚举值时抛出。</exception>
    public static T GetEnumValueByInt<T>(int intValue) where T : struct, Enum
    {
        if (Enum.IsDefined(typeof(T), intValue))
        {
            return (T)Enum.ToObject(typeof(T), intValue);
        }
        throw new NotSupportedException($"{typeof(T).Name} 尚不支持整数值：'{intValue}'");
    }

    /// <summary>
    /// 获取 DisplayName 到枚举值的映射。
    /// </summary>
    /// <param name="type">枚举类型。</param>
    /// <returns>DisplayName 到枚举值的映射。</returns>
    private static Dictionary<string, List<Enum>> GetDisplayToEnumValuesMap(Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));
        return DisplayNameToEnumValuesCache.GetOrAdd(type, t =>
        {
            var map = new Dictionary<string, List<Enum>>();
            var values = Enum.GetValues(t);
            foreach (var value in values)
            {
                var enumValue = (Enum)value;
                var displayName = enumValue.GetDisplayAttribute()?.Name;
                if (displayName != null)
                {
                    if (!map.TryGetValue(displayName, out var enumValueList))
                    {
                        enumValueList = [];
                        map[displayName] = enumValueList;
                    }
                    enumValueList.Add(enumValue);
                }
            }
            return map;
        });
    }

    /// <summary>
    /// 检查指定的枚举值是否是有效的枚举值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="enumValue">要检查的枚举值。</param>
    /// <returns>如果是有效的枚举值，返回 true；否则返回 false。</returns>
    public static bool IsValidEnumValue<T>(T enumValue) where T : struct, Enum
        => Enum.IsDefined(enumValue);

    /// <summary>
    /// 检查指定的枚举值名称是否有效。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="valueName">枚举值名称。</param>
    /// <returns>如果有效，返回 true；否则返回 false。</returns>
    public static bool IsValidEnumName<T>(string valueName) where T : struct, Enum
        => Enum.GetNames<T>().Contains(valueName);

    /// <summary>
    /// 检查指定的显示文本是否对应有效的枚举值显示名称。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <param name="displayText">显示文本。</param>
    /// <returns>如果有效，返回 true；否则返回 false。</returns>
    public static bool IsValidEnumDisplay<T>(string displayText) where T : struct, Enum
        => GetEnumDisplayNames<T>().Contains(displayText);

    /// <summary>
    /// 随机返回一个枚举值。
    /// </summary>
    /// <typeparam name="T">枚举类型。</typeparam>
    /// <returns>随机的枚举值。</returns>
    public static T GetRandomEnumValue<T>() where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = RandomInstance.Next(values.Length);
        return (T)values.GetValue(index)!;
    }

    /// <summary>
    /// 清理所有缓存（用于单元测试或热更新场景）。
    /// </summary>
    public static void ClearCache()
    {
        EnumValueDisplayCache.Clear();
        AllDisplayNamesCache.Clear();
        DisplayNameToEnumValuesCache.Clear();
    }
}