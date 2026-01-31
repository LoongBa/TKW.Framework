using System;
using System.Security.Cryptography;
using System.Text;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Cryptography
{
    /// <summary>
    /// 算法相关的方法
    /// </summary>
    /// <remarks>默认使用 UTF8</remarks>
    public static class SymmetricAlgorithmHelper
    {
        #region 对称加密解密

        /// <summary>
        /// 对称加密
        /// </summary>
        /// <param name="algorithm">算法类型</param>
        /// <param name="clearText">明文</param>
        /// <param name="key">密钥，长度必须32位</param>
        /// <param name="vector">向量，长度必须是16位</param>
        /// <param name="encoding"></param>
        /// <returns>加密后的字节数组</returns>
        public static string SymmetricEncrypt(SymmetricAlgorithm algorithm, string clearText, string key, string vector, Encoding encoding = null)
        {
            return SymmetricEncrypt(algorithm, clearText, key.GetBytes(encoding), vector.GetBytes(encoding), encoding);
        }

        /// <summary>
        /// 对称加密
        /// </summary>
        /// <param name="algorithm">算法类型</param>
        /// <param name="clearText">明文</param>
        /// <param name="key">密钥，长度必须32位</param>
        /// <param name="vector">向量，长度必须是16位</param>
        /// <param name="encoding"></param>
        /// <returns>加密后的字节数组</returns>
        public static string SymmetricEncrypt(SymmetricAlgorithm algorithm, string clearText, byte[] key, byte[] vector, Encoding encoding = null)
        {
            algorithm.EnsureNotNull(name: nameof(algorithm));
            clearText.EnsureHasValue(nameof(clearText));
            key.EnsureNotNull(name: nameof(key));
            vector.EnsureNotNull(name: nameof(vector));

            if (key.Length != 32) throw new ArgumentOutOfRangeException(nameof(key), "密钥长度必须为32位");
            if (vector.Length != 16) throw new ArgumentOutOfRangeException(nameof(key), "向量长度必须为16位");

            using var symmetricAlgorithm = algorithm;
            symmetricAlgorithm.Key = key;
            symmetricAlgorithm.IV = vector;
            using var cryptoTransform = symmetricAlgorithm.CreateEncryptor();
            var inputBuffers = clearText.GetBytes(encoding);
            return cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length).GetString(encoding);
        }

        /// <summary>
        /// 对称解密
        /// </summary>
        /// <param name="algorithm">算法类型</param>
        /// <param name="cipherText">密文</param>
        /// <param name="key">密钥，长度必须32位</param>
        /// <param name="vector">向量，长度必须是16位</param>
        /// <param name="encoding"></param>
        /// <returns>解密后的字符串</returns>
        public static string SymmetricDecrypt(SymmetricAlgorithm algorithm, string cipherText, string key, string vector, Encoding encoding = null)
        {
            return SymmetricDecrypt(algorithm, cipherText, key.GetBytes(encoding), vector.GetBytes(encoding), encoding);
        }

        /// <summary>
        /// 对称解密
        /// </summary>
        /// <param name="algorithm">算法类型</param>
        /// <param name="cipherText">密文</param>
        /// <param name="key">密钥，长度必须32位</param>
        /// <param name="vector">向量，长度必须是16位</param>
        /// <param name="encoding"></param>
        /// <returns>解密后的字符串</returns>
        public static string SymmetricDecrypt(SymmetricAlgorithm algorithm, string cipherText, byte[] key, byte[] vector, Encoding encoding = null)
        {
            cipherText.EnsureHasValue(nameof(cipherText));
            key.EnsureNotNull(name: nameof(key));
            vector.EnsureNotNull(name: nameof(vector));

            if (key.Length != 32) throw new ArgumentOutOfRangeException(nameof(key), "密钥长度必须为32位");
            if (vector.Length != 16) throw new ArgumentOutOfRangeException(nameof(key), "向量长度必须为16位");

            using var symmetricAlgorithm = algorithm;
            symmetricAlgorithm.Key = key;
            symmetricAlgorithm.IV = vector;
            using var cryptoTransform = symmetricAlgorithm.CreateDecryptor();
            var inputBuffers = cipherText.GetBytes(encoding);
            return cryptoTransform.TransformFinalBlock(inputBuffers, 0, inputBuffers.Length).GetString(encoding);
        }
        #endregion
    }
}
