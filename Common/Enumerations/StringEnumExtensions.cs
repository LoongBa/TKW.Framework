using System;

namespace TKW.Framework.Common.Enumerations;

/// <summary>
/// 字符串类型的枚举扩展方法。
/// </summary>
public static class StringEnumExtensions
{
    /// <param name="value">字符串。</param>
    extension(string value)
    {
        /// <summary>
        /// 将字符串解析为枚举值（基于 DisplayName 特性）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>枚举值。</returns>
        public T ToEnumByDisplay<T>() where T : struct, Enum
            => EnumHelper.ParseEnumValueByDisplay<T>(value);

        /// <summary>
        /// 将字符串解析为枚举值（基于枚举名称）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>枚举值。</returns>
        public T ToEnum<T>() where T : struct, Enum
            => EnumHelper.Parse<T>(value);

        /// <summary>
        /// 将字符串解析为枚举值（基于枚举名称，失败时返回默认值）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>枚举值。</returns>
        public T ToEnum<T>(T defaultValue) where T : struct, Enum
            => EnumHelper.Parse(value, defaultValue);

        /// <summary>
        /// 检查字符串是否匹配枚举的名字。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>是否有效。</returns>
        public bool IsValidEnumName<T>() where T : struct, Enum
            => EnumHelper.IsValidEnumName<T>(value);

        /// <summary>
        /// 检查字符串是否匹配枚举的 DisplayName。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <returns>是否有效。</returns>
        public bool IsValidEnumDisplay<T>() where T : struct, Enum
            => EnumHelper.IsValidEnumDisplay<T>(value);
    }
}