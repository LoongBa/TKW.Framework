using System.Security.Cryptography;

namespace TKW.Framework.Text
{
    public static class RandomUtil
    {
        private const string AlphanumericChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const string NumericChars = "0123456789";

        /// <summary>
        /// 生成高度安全的随机字符串 (替代原 Generate16RandomString 等方法)
        /// </summary>
        public static string GenerateString(int length, string choices = AlphanumericChars)
        {
            return RandomNumberGenerator.GetString(choices, length);
        }

        /// <summary>
        /// 生成指定长度的随机数字 (如验证码)
        /// </summary>
        public static string GenerateNumericCode(int length)
        {
            return RandomNumberGenerator.GetString(NumericChars, length);
        }
    }
}