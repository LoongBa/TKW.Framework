using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 枚举默认项标记特性（可选扩展，用于明确标记业务默认枚举项）
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class EnumDefaultAttribute : Attribute
{
}

/*
// 1. 枚举定义（带 DisplayAttribute 和 EnumDefaultAttribute）
   public enum PayStatus
   {
       [Display(Name = "未支付")]
       [EnumDefault]
       Unpaid = 0,
   
       [Display(Name = "已支付")]
       Paid = 1,
   
       [Display(Name = "部分支付")]
       PartPaid = 2,
   
       [Display(Name = "已退款")]
       Refunded = 3
   }
   
   // 2. 多维度解析
   var status1 = EnumHelper.ParseEnum<PayStatus>("1"); // 数值匹配 → Paid
   var status2 = EnumHelper.ParseEnum<PayStatus>("Paid"); // 英文名匹配 → Paid
   var status3 = EnumHelper.ParseEnum<PayStatus>("已支付"); // DisplayName匹配 → Paid
   var status4 = EnumHelper.ParseEnum<PayStatus>("无效值", PayStatus.Unpaid); // 匹配失败 → 自定义默认值 Unpaid
   
   // 3. 获取 DisplayName
   var displayName = EnumHelper.GetEnumValueDisplayName(PayStatus.Paid); // → "已支付"
   
   // 4. 校验
   var isValid = EnumHelper.IsValidEnumDisplay<PayStatus>("未支付"); // → true
   
   // 5. 整数转换
   var intValue = EnumHelper.GetIntValue(PayStatus.Paid); // → 1
   var enumValue = EnumHelper.GetEnumValueByInt<PayStatus>(1); // → Paid
 */

/// <summary>
/// 枚举工具类，提供基于 数值/英文名/DisplayAttribute.Name 的多维度解析、映射、校验等功能。
/// 特性支持：DisplayAttribute（原生）、EnumDefaultAttribute（自定义默认项）
/// 线程安全：基于 ConcurrentDictionary 实现缓存安全
/// 匹配规则：数值 > 英文名（大小写不敏感） > DisplayName（大小写不敏感）
/// 默认值规则：传入自定义默认值 > 数值0枚举项 > 第一个定义项 > EnumDefault特性标记项
/// </summary>
public static class EnumHelper
{
    #region 缓存容器（线程安全）
    /// <summary>
    /// 枚举值 -> DisplayName 缓存（键：枚举Type，值：枚举对象 -> 显示名）
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<object, string>> EnumValueDisplayCache = new();

    /// <summary>
    /// 枚举所有 DisplayName 列表缓存（键：枚举Type，值：显示名列表）
    /// </summary>
    private static readonly ConcurrentDictionary<Type, List<string>> AllDisplayNamesCache = new();

    /// <summary>
    /// DisplayName -> 枚举值列表缓存（键：枚举Type，值：显示名 -> 枚举对象列表，大小写不敏感）
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, List<Enum>>> DisplayNameToEnumValuesCache =
        new ConcurrentDictionary<Type, Dictionary<string, List<Enum>>>();

    /// <summary>
    /// 随机数实例（线程安全）
    /// </summary>
    private static readonly Random RandomInstance = new Random();
    #endregion

    #region 核心：多维度枚举解析（支持默认值兜底）
    /// <summary>
    /// 通用枚举解析（按 数值 → 英文名 → DisplayName 优先级匹配，支持自定义默认值）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="input">输入字符串（数值/英文名/DisplayAttribute.Name）</param>
    /// <param name="defaultValue">自定义默认值（匹配失败时返回）</param>
    /// <returns>解析后的枚举值</returns>
    public static T ParseEnum<T>(string input, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        var inputTrimmed = input.Trim();

        // 1. 优先匹配：枚举数值（int类型，无歧义）
        if (int.TryParse(inputTrimmed, out var intValue) && Enum.IsDefined(typeof(T), intValue))
        {
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        // 2. 其次匹配：枚举英文名（大小写不敏感）
        if (Enum.TryParse(inputTrimmed, true, out T enumResult))
        {
            return enumResult;
        }

        // 3. 最后匹配：DisplayAttribute.Name（大小写不敏感）
        var displayMatches = GetEnumValuesByDisplay<T>(inputTrimmed);
        if (displayMatches.Count > 0)
        {
            return displayMatches[0];
        }

        // 4. 所有匹配失败，返回自定义默认值
        return defaultValue;
    }

    /// <summary>
    /// 通用枚举解析（无自定义默认值时，按 数值0 → 第一个项 → EnumDefault特性 兜底）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="input">输入字符串（数值/英文名/DisplayAttribute.Name）</param>
    /// <returns>解析后的枚举值</returns>
    /// <exception cref="ArgumentException">输入为空字符串时抛出</exception>
    public static T ParseEnum<T>(string input) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input, nameof(input));
        return ParseEnum(input, GetEnumFallbackDefault<T>());
    }

    /// <summary>
    /// 根据 DisplayName 解析枚举（仅匹配 DisplayAttribute.Name，支持自定义默认值）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="displayName">DisplayAttribute.Name 字符串</param>
    /// <param name="defaultValue">自定义默认值</param>
    /// <returns>解析后的枚举值</returns>
    public static T ParseEnumValueByDisplay<T>(string displayName, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return defaultValue;

        var displayMatches = GetEnumValuesByDisplay<T>(displayName.Trim());
        return displayMatches.Count > 0 ? displayMatches[0] : defaultValue;
    }

    /// <summary>
    /// 根据 DisplayName 解析枚举（仅匹配 DisplayAttribute.Name，无匹配时抛出异常）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="displayName">DisplayAttribute.Name 字符串</param>
    /// <returns>解析后的枚举值</returns>
    /// <exception cref="ArgumentException">输入为空时抛出</exception>
    /// <exception cref="NotSupportedException">无匹配枚举值时抛出</exception>
    public static T ParseEnumValueByDisplay<T>(string displayName) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName, nameof(displayName));

        var displayMatches = GetEnumValuesByDisplay<T>(displayName.Trim());
        if (displayMatches.Count > 0)
        {
            return displayMatches[0];
        }

        throw new NotSupportedException($"{typeof(T).Name} 尚不支持 DisplayName：'{displayName}'");
    }

    /// <summary>
    /// 仅按枚举英文名解析（原生 Enum.TryParse 封装，支持自定义默认值）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumName">枚举英文名</param>
    /// <param name="defaultValue">自定义默认值</param>
    /// <returns>解析后的枚举值</returns>
    public static T Parse<T>(string enumName, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(enumName))
            return defaultValue;

        return Enum.TryParse(enumName, out T result) ? result : defaultValue;
    }

    /// <summary>
    /// 仅按枚举英文名解析（原生 Enum.TryParse 封装，无匹配时抛出异常）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumName">枚举英文名</param>
    /// <returns>解析后的枚举值</returns>
    /// <exception cref="ArgumentException">输入无效或无匹配时抛出</exception>
    public static T Parse<T>(string enumName) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enumName, nameof(enumName));

        if (Enum.TryParse(enumName, out T result))
        {
            return result;
        }

        throw new ArgumentException($"字符串 '{enumName}' 无法解析为枚举类型 {typeof(T).Name}。");
    }
    #endregion

    #region 枚举 <-> DisplayName 映射
    /// <summary>
    /// 获取枚举所有值对应的 DisplayName 列表
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>DisplayName 列表（无 DisplayAttribute 则不包含对应项）</returns>
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
                    var displayName = GetDisplayAttribute((Enum)value)?.Name;
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        names.Add(displayName);
                    }
                }
                catch
                {
                    // 静默失败，不添加无效项
                }
            }

            return names;
        });
    }

    /// <summary>
    /// 获取单个枚举值对应的 DisplayName（无 DisplayAttribute 则返回枚举英文名）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>DisplayName 或 枚举英文名</returns>
    public static string GetEnumValueDisplayName<T>(T enumValue) where T : struct, Enum
    {
        var displayMap = GetEnumValueDisplayMap<T>();
        return displayMap.TryGetValue(enumValue, out var displayName) ? displayName : enumValue.ToString();
    }

    /// <summary>
    /// 获取枚举值与 DisplayName 的强类型映射字典
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>枚举值 -> DisplayName 字典</returns>
    public static Dictionary<T, string> GetEnumValueDisplayMap<T>() where T : struct, Enum
    {
        var enumType = typeof(T);
        var cacheDict = EnumValueDisplayCache.GetOrAdd(enumType, t =>
        {
            var dict = new Dictionary<object, string>();
            var values = Enum.GetValues<T>();

            foreach (var value in values)
            {
                try
                {
                    var displayName = GetDisplayAttribute((Enum)(object)value)?.Name;
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        dict[value] = displayName;
                    }
                }
                catch
                {
                    // 静默失败，不添加无效项
                }
            }

            return dict;
        });

        // 转换为强类型字典，避免装箱/拆箱开销
        return cacheDict
            .Where(kv => kv.Key is T)
            .ToDictionary(kv => (T)kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// 根据 DisplayName 获取对应的枚举值列表（支持一对多映射）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="displayName">DisplayAttribute.Name 字符串</param>
    /// <returns>枚举值列表（无匹配则返回空列表）</returns>
    public static List<T> GetEnumValuesByDisplay<T>(string displayName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new List<T>();
        }

        var displayMap = GetDisplayToEnumValuesMap(typeof(T));
        if (displayMap.TryGetValue(displayName.Trim(), out var enumValues))
        {
            return enumValues.Cast<T>().ToList();
        }

        return new List<T>();
    }
    #endregion

    #region 枚举 <-> 整数 转换
    /// <summary>
    /// 获取枚举值对应的整数值
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>整数类型值</returns>
    public static int GetIntValue<T>(T enumValue) where T : struct, Enum
    {
        return Convert.ToInt32(enumValue);
    }

    /// <summary>
    /// 根据整数值获取枚举值
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="intValue">整数类型值</param>
    /// <returns>枚举值</returns>
    /// <exception cref="NotSupportedException">无对应枚举值时抛出</exception>
    public static T GetEnumValueByInt<T>(int intValue) where T : struct, Enum
    {
        if (Enum.IsDefined(typeof(T), intValue))
        {
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        throw new NotSupportedException($"{typeof(T).Name} 尚不支持整数值：'{intValue}'");
    }
    #endregion

    #region 枚举校验
    /// <summary>
    /// 检查枚举值是否有效（是否在枚举定义范围内）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>有效返回 true，无效返回 false</returns>
    public static bool IsValidEnumValue<T>(T enumValue) where T : struct, Enum
    {
        return Enum.IsDefined(enumValue);
    }

    /// <summary>
    /// 检查枚举英文名是否有效
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumName">枚举英文名</param>
    /// <returns>有效返回 true，无效返回 false</returns>
    public static bool IsValidEnumName<T>(string enumName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        return Enum.GetNames<T>().Contains(enumName);
    }

    /// <summary>
    /// 检查 DisplayName 是否对应有效的枚举显示名
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="displayText">Display Name 字符串</param>
    /// <returns>有效返回 true，无效返回 false</returns>
    public static bool IsValidEnumDisplay<T>(string displayText) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(displayText))
            return false;

        return GetEnumDisplayNames<T>().Contains(displayText, StringComparer.OrdinalIgnoreCase);
    }
    #endregion

    #region 辅助功能
    /// <summary>
    /// 随机返回一个有效的枚举值
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>随机枚举值</returns>
    public static T GetRandomEnumValue<T>() where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = RandomInstance.Next(values.Length);
        return values[index];
    }

    /// <summary>
    /// 清理所有枚举缓存（用于单元测试、热更新场景）
    /// </summary>
    public static void ClearCache()
    {
        EnumValueDisplayCache.Clear();
        AllDisplayNamesCache.Clear();
        DisplayNameToEnumValuesCache.Clear();
    }
    #endregion

    #region 私有辅助方法
    /// <summary>
    /// 获取枚举字段的 DisplayAttribute 特性
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>DisplayAttribute 或 null</returns>
    private static DisplayAttribute GetDisplayAttribute(Enum enumValue)
    {
        if (enumValue == null)
            return null;

        var field = enumValue.GetType().GetField(enumValue.ToString());
        return field?.GetCustomAttribute<DisplayAttribute>();
    }

    /// <summary>
    /// 获取 DisplayName -> 枚举值列表的映射（大小写不敏感）
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>DisplayName 映射字典</returns>
    /// <exception cref="ArgumentNullException">枚举类型为 null 时抛出</exception>
    private static Dictionary<string, List<Enum>> GetDisplayToEnumValuesMap(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType, nameof(enumType));

        return DisplayNameToEnumValuesCache.GetOrAdd(enumType, t =>
        {
            // 大小写不敏感字典，兼容更多业务场景
            var map = new Dictionary<string, List<Enum>>(StringComparer.OrdinalIgnoreCase);
            var values = Enum.GetValues(t);

            foreach (var value in values)
            {
                var enumValue = (Enum)value;
                var displayName = GetDisplayAttribute(enumValue)?.Name;

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    if (!map.TryGetValue(displayName, out var enumValueList))
                    {
                        enumValueList = new List<Enum>();
                        map[displayName] = enumValueList;
                    }

                    enumValueList.Add(enumValue);
                }
            }

            return map;
        });
    }

    /// <summary>
    /// 获取枚举兜底默认值（优先级：数值0项 → 第一个定义项 → EnumDefault特性标记项）
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>兜底默认值</returns>
    private static T GetEnumFallbackDefault<T>() where T : struct, Enum
    {
        // 1. 优先返回数值0的枚举项（C#原生默认值）
        var defaultValue = default(T);
        if (Enum.IsDefined(defaultValue))
        {
            return defaultValue;
        }

        // 2. 其次返回第一个定义的枚举项
        var values = Enum.GetValues<T>();
        if (values.Length > 0)
        {
            // 检查第一个项是否有 EnumDefault 特性，无则直接返回第一个项
            var firstValue = values[0];
            var hasDefaultAttr = GetEnumDefaultAttribute(firstValue) != null;
            if (hasDefaultAttr)
            {
                return firstValue;
            }
        }

        // 3. 最后返回 EnumDefault 特性标记的项
        foreach (var value in values)
        {
            if (GetEnumDefaultAttribute(value) != null)
            {
                return value;
            }
        }

        // 极端场景：无任何有效项，返回默认值（不会触发，枚举必有定义项）
        return defaultValue;
    }

    /// <summary>
    /// 获取枚举字段的 EnumDefaultAttribute 特性
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>EnumDefaultAttribute 或 null</returns>
    private static EnumDefaultAttribute GetEnumDefaultAttribute<T>(T enumValue) where T : struct, Enum
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        return field?.GetCustomAttribute<EnumDefaultAttribute>();
    }
    #endregion
}