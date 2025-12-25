using System;

namespace TKW.Framework.Common.Extensions;

public static class StringExtensions
{
    #region 字符串

    /// <param name="left">字符串</param>
    extension(string left)
    {
        /// <summary>
        /// 字符串是否含有有效值（非 null、非空格、非 Empty）
        /// </summary>
        public bool HasValue()
            => !string.IsNullOrEmpty(left) && !string.IsNullOrWhiteSpace(left);

        /// <summary>
        /// 如果没有有效值（非 null、非空格、非 Empty）则转为 null
        /// </summary>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        public string HasNoValueToNull(bool toTrim = true)
            => !left.HasValue()
                ? null
                : toTrim
                    ? left.Trim()
                    : left;

        /// <summary>
        /// 如果没有有效值（非 null、非空格、非 Empty）则转为 string.Empty
        /// </summary>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        public string HasNoValueToEmpty(bool toTrim = true)
            => !left.HasValue()
                ? string.Empty
                : toTrim
                    ? left.Trim()
                    : left;

        /// <summary>
        /// null 转为 String.Empty
        /// </summary>
        public string NullToEmpty()
            => left ?? string.Empty;

        /// <summary>
        /// 字符串未含有有效值则指定默认值（改变值）
        /// </summary>
        /// <param name="defaultString">自定义消息</param>
        /// <param name="name">自定义参数名</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        /// <exception cref="ArgumentException"></exception>
        public string DefaultValue(string defaultString,
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
        /// <param name="message">自定义消息</param>
        /// <param name="name">自定义参数名</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        /// <exception cref="ArgumentNullException"></exception>
        public string EnsureHasValue(string message = null,
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
        public string TrimSelf()
        {
            return left.HasValue() ? left.Trim() : left;
        }
    }

    #endregion

    #region FormatWith

    extension(string format)
    {
        public string FormatWith(params object[] args)
        {
            return string.Format(format.EnsureHasValue(), args);
        }

        public string FormatWith(IFormatProvider provider, params object[] args)
        {
            return string.Format(provider, format.EnsureHasValue(), args);
        }
    }

    #endregion
}