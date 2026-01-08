using System;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 枚举类型的扩展方法。
/// </summary>
public static class EnumExtensions
{
    /// <param name="value">枚举值。</param>
    /// <typeparam name="T">枚举类型。</typeparam>
    extension<T>(T value) where T : struct, Enum
    {
        /// <summary>
        /// 获取枚举值的 DisplayName 特性值。
        /// </summary>
        /// <returns>DisplayName。</returns>
        public string GetDisplayName() => EnumHelper.GetEnumValueDisplayName(value);

        /// <summary>
        /// 获取枚举值对应的整数值。
        /// </summary>
        /// <returns>整数值。</returns>
        public int ToInt() => EnumHelper.GetIntValue(value);

        /// <summary>
        /// 检查枚举值是否为定义的值。
        /// </summary>
        /// <returns>是否有效。</returns>
        public bool IsValid() => EnumHelper.IsValidEnumValue(value);
    }
}