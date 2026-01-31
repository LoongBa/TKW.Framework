using System;
using System.Text.RegularExpressions;

namespace TKW.Framework.Common.Extensions;

public static class StringRegexExtensions
{
    #region Regex

    /// <param name="left">字符串</param>
    extension(string left)
    {
        /// <summary>
        /// 断言字符串符合规则
        /// </summary>
        /// <param name="regexPattern"></param>
        /// <param name="message">自定义消息</param>
        /// <param name="name">自定义参数名</param>
        /// <param name="toTrim">是否去掉头尾包含的空字符串（默认去掉）</param>
        /// <exception cref="ArgumentException"></exception>
        public string AssertValueIsMatch(string regexPattern, string message = null, string name = null,
            bool toTrim = true)
        {
            name = name.HasValue() ? name : nameof(left);
            if (left.IsMatch(regexPattern))
                //TODO: 提取到资源文件
                throw new ArgumentException(message ?? $"参数 ‘{name}’ 不符合指定的规则 '{regexPattern}'", name);

            //不要改变 left 的值，交给调用者决定
            return toTrim ? left.Trim() : left;
        }

        public bool IsMatch(string regexPattern)
            => Regex.IsMatch(left.EnsureHasValue(), regexPattern.EnsureHasValue());

        public string Replace(string regexPattern, string replacement)
            => Regex.Replace(
                left.EnsureHasValue(),
                regexPattern.EnsureHasValue(),
                replacement.EnsureHasValue());

        public bool IsMatch(Regex regexPattern)
        {
            return regexPattern.IsMatch(left.EnsureHasValue());
        }

        public string RegexMatch(Regex regex)
        {
            if (string.IsNullOrEmpty(left)) throw new ArgumentNullException();
            return regex.Match(left).Value;
        }

        public string Replace(Regex regex, string replacement)
        {
            if (string.IsNullOrEmpty(left)) throw new ArgumentNullException();
            return regex.EnsureNotNull().Replace(left, replacement);
        }
    }

    #endregion
}