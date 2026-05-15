using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography
{
    /// <summary>
    /// 提供高性能的现代哈希方法
    /// </summary>
    public static class HashUtil
    {
        /// <summary>
        /// 计算 SHA256 (推荐用于普通数据防篡改校验)
        /// </summary>
        public static string ComputeSha256(string data, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(data);
            encoding ??= Encoding.UTF8;

            // .NET 5+ 优化写法：一步到位计算并转大写十六进制，且无多余内存分配
            var hashBytes = SHA256.HashData(encoding.GetBytes(data));
            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// 计算 MD5 (警告：仅用于兼容老旧系统或普通文件校验，绝不可用于密码或高安全性签名)
        /// </summary>
        public static string ComputeMd5(string data, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(data);
            encoding ??= Encoding.UTF8;

            var hashBytes = MD5.HashData(encoding.GetBytes(data));
            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// 验证哈希是否匹配（安全的时间恒定比较，防止时序攻击）
        /// </summary>
        public static bool VerifyHash(string plainText, string expectedHexHash, Func<string, Encoding?, string> hashFunc, Encoding? encoding = null)
        {
            var actualHash = hashFunc(plainText, encoding);
            // 使用 CryptographicOperations.FixedTimeEquals 防止时序攻击
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(expectedHexHash));
        }
    }
}