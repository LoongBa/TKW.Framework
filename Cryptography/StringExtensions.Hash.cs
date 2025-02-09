using System.Text;

namespace TKW.Framework.Cryptography
{
    public static class HashStringExtensions
    {
        #region Hash 字符串相关的扩展

        public static string Hash(this string left, string salt, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Md5, Encoding encoding = null)
        {
            return CryptographyHelper.Hash(hashAlgorithmType, left, salt, encoding);
        }
        public static string Hash(this string left, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Md5, Encoding encoding = null)
        {
            return CryptographyHelper.Hash(hashAlgorithmType, left, encoding);
        }

        public static bool IsHashMatch(this string unhashedText, string hashedText, string salt, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Md5, Encoding encoding = null)
        {
            return CryptographyHelper.IsHashMatch(hashAlgorithmType, hashedText, unhashedText, salt, encoding);
        }
        public static bool IsHashMatch(this string unhashedText, string hashedText, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Md5, Encoding encoding = null)
        {
            return CryptographyHelper.IsHashMatch(hashAlgorithmType, hashedText, unhashedText, encoding);
        }

        #endregion
    }
}