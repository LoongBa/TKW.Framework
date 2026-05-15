using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography
{
    /// <summary>
    /// 提供基于 AES-GCM 的高级对称加密 (自带防篡改认证)
    /// </summary>
    public static class AeadEncryptionUtil
    {
        private const int NonceSize = 12; // AES-GCM 推荐 Nonce 长度为 12 字节
        private const int TagSize = 16;   // 认证标签长度为 16 字节

        /// <summary>
        /// 加密并返回包含 Nonce 和 Tag 的 Base64 字符串
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="keyBase64">32字节密钥的Base64形式</param>
        public static string Encrypt(string plainText, string keyBase64)
        {
            var key = Convert.FromBase64String(keyBase64);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            // 分配连续内存: Nonce (12) + CipherText (N) + Tag (16)
            var result = new byte[NonceSize + plainBytes.Length + TagSize];
            Span<byte> resultSpan = result;

            // 1. 生成随机 Nonce
            var nonceSpan = resultSpan.Slice(0, NonceSize);
            RandomNumberGenerator.Fill(nonceSpan);

            // 2. 切分 Cipher 和 Tag 区域
            var cipherSpan = resultSpan.Slice(NonceSize, plainBytes.Length);
            var tagSpan = resultSpan.Slice(NonceSize + plainBytes.Length, TagSize);

            // 3. 执行 GCM 加密
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonceSpan, plainBytes, cipherSpan, tagSpan);

            // 必须返回 Base64，严禁使用 Encoding.UTF8.GetString 转换密文！
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// 解密
        /// </summary>
        public static string Decrypt(string cipherTextBase64, string keyBase64)
        {
            var key = Convert.FromBase64String(keyBase64);
            var encryptedData = Convert.FromBase64String(cipherTextBase64);
            ReadOnlySpan<byte> dataSpan = encryptedData;

            if (dataSpan.Length < NonceSize + TagSize)
                throw new CryptographicException("密文长度无效。");

            var nonceSpan = dataSpan.Slice(0, NonceSize);
            var tagSpan = dataSpan.Slice(dataSpan.Length - TagSize, TagSize);
            var cipherSpan = dataSpan.Slice(NonceSize, dataSpan.Length - NonceSize - TagSize);

            var plainBytes = new byte[cipherSpan.Length];

            // 执行解密，如果密文被篡改，这里会直接抛出 CryptographicException
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonceSpan, cipherSpan, tagSpan, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}