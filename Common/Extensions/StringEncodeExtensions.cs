using System;
using System.Linq;
using System.Text;

namespace TKW.Framework.Common.Extensions
{
    public static class StringEncodeExtensions
    {
        private static readonly Encoding DefaultEncoding = Encoding.UTF8;
        #region byte[]、十六进制、Base64相关
        /// <summary>
        /// 转为字节数组
        /// </summary>
        public static byte[] GetBytes(this string left, Encoding encoding = null)
        {
            encoding ??= DefaultEncoding;
            return encoding.GetBytes(left.EnsureHasValue());
        }

        /// <summary>
        /// 字节数组转为字符串
        /// </summary>
        public static string GetString(this byte[] left, Encoding encoding = null)
        {
            encoding ??= DefaultEncoding;
            return encoding.GetString(left);
        }

        /// <summary>
        /// 对字符串进行 Base64 编码
        /// </summary>
        /// <param name="left"></param>
        /// <param name="encoding">指定编码，默认为 UTF8</param>
        public static string ToBase64(this string left, Encoding encoding = null)
        {
            return Convert.ToBase64String(left.GetBytes(encoding));
        }

        /// <summary>
        /// 对字符串进行 Base64 解码
        /// </summary>
        /// <param name="left"></param>
        /// <param name="encoding">指定编码，默认为 UTF8</param>
        public static string FromBase64(this string left, Encoding encoding = null)
        {
            return Convert.FromBase64String(left.EnsureHasValue()).GetString(encoding);
        }

        /// <summary>
        /// 将指定字符串转换为 Hex 十六进制字符串
        /// </summary>
        /// <param name="left"></param>
        /// <param name="encoding">指定编码，默认为 UTF8</param>
        public static string ToHexString(this string left, Encoding encoding = null)
        {
            var result = string.Empty;
            return left.GetBytes(encoding)
                .Aggregate(result, (current, item) => current + item.ToString("X2"));
        }

        /// <summary>
        /// 将指定二进制数组转换为 Hex 十六进制字符串
        /// </summary>
        /// <param name="left"></param>
        public static string ToHexString(this byte[] left)
        {
            var result = string.Empty;
            return left.Aggregate(result, (current, item) => current + item.ToString("X2"));
        }

        #endregion
    }
}