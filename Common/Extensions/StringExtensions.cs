using System;

namespace TKW.Framework.Common.Extensions
{
    public static class StringExtensions
    {
        #region 字符串

        /// <summary>
        /// 字符串是否含有有效值（非 null、非空格、非 Empty）
        /// </summary>
        public static bool HasValue(this string left)
            => !string.IsNullOrEmpty(left) && !string.IsNullOrWhiteSpace(left);

        /// <summary>
        /// 如果没有有效值（非 null、非空格、非 Empty）则转为 null
        /// </summary>
        /// <param name="left">字符串</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        public static string HasNoValueToNull(this string left, bool toTrim = true)
            => !left.HasValue()
                ? null
                : toTrim
                    ? left.Trim()
                    : left;
        /// <summary>
        /// 如果没有有效值（非 null、非空格、非 Empty）则转为 string.Empty
        /// </summary>
        /// <param name="left">字符串</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        public static string HasNoValueToEmpty(this string left, bool toTrim = true)
            => !left.HasValue()
                ? string.Empty
                : toTrim
                    ? left.Trim()
                    : left;

        /// <summary>
        /// null 转为 String.Empty
        /// </summary>
        public static string NullToEmpty(this string left)
            => left ?? string.Empty;

        /// <summary>
        /// 字符串未含有有效值则指定默认值（改变值）
        /// </summary>
        /// <param name="left">字符串</param>
        /// <param name="defaultString">自定义消息</param>
        /// <param name="name">自定义参数名</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        /// <exception cref="ArgumentException"></exception>
        public static string DefaultValue(this string left, string defaultString,
            bool toTrim = true)
        {
            if (!left.HasValue())
                left = defaultString.EnsureHasValue();

            left = toTrim ? left.Trim() : left;
            return left;
        }

        /// <summary>
        /// 确保字符串含有有效值（改变值）
        /// </summary>
        /// <param name="left">字符串</param>
        /// <param name="message">自定义消息</param>
        /// <param name="name">自定义参数名</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static string EnsureHasValue(this string left, string message = null,
            string name = null, bool toTrim = true)
        {
            name = name.HasValue() ? name : nameof(left);
            if (!left.HasValue())
            {
                message = message ?? $"参数 ‘{name}’ 不能为 null、空字符串、仅含空白的字符串";
                throw new ArgumentNullException(name, message);
            }

            //不要改变 left 的值，交给调用者决定
            return toTrim ? left.Trim() : left;
        }

        /// <summary>
        /// 去掉前后的多余空格
        /// </summary>
        public static string TrimSelf(this string left)
        {
            return left.HasValue() ? left.Trim() : left;
        }

        #endregion

        #region FormatWith

        public static string FormatWith(this string format, params object[] args)
        {
            return string.Format(format.EnsureHasValue(), args);
        }

        public static string FormatWith(this string format, IFormatProvider provider, params object[] args)
        {
            return string.Format(provider, format.EnsureHasValue(), args);
        }

        #endregion
    }
}