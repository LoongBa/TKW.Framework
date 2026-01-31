using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Cryptography
{
    /// <summary>
    /// 算法相关的方法
    /// </summary>
    /// <remarks>默认使用 UTF8</remarks>
    public static class CryptographyHelper
    {
        public static string GenerateRandomString(uint minLength = 5, uint maxLength = 10)
        {
            var strSep = ",";
            var chrSep = strSep.ToCharArray();

            var strChar = "1,2,3,4,5,6,7,8,9,0,A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z";
            var aryChar = strChar.Split(chrSep, strChar.Length);

            var strRandom = string.Empty;
            var indexRandom = new Random(Environment.TickCount);
            var lengthRandom = new Random(Environment.TickCount);

            if (minLength == 0) minLength = 5;
            if (maxLength == 0 || maxLength < minLength) maxLength = minLength;

            int count;
            count = minLength == maxLength
                ? lengthRandom.Next((int)minLength)
                : lengthRandom.Next((int)minLength, (int)maxLength);

            //生成随机字符串
            for (var i = 0; i < count; i++)
            {
                strRandom += aryChar[indexRandom.Next(36)];
            }
            return strRandom;
        }


        #region Hash 通用方法

        public static string Hash(HashAlgorithmType hashAlgorithmType, string dataToHash, string salt, Encoding encoding = null)
        {
            salt.EnsureHasValue(nameof(salt));
            dataToHash = $"{dataToHash}@{salt}";
            return Hash(hashAlgorithmType, dataToHash, encoding);
        }

        public static string Hash(HashAlgorithmType hashAlgorithmType, string dataToHash, Encoding encoding = null)
        {
            dataToHash.EnsureHasValue(nameof(dataToHash));
            var hashAlgorithm = HashAlgorithmFactory.Create(hashAlgorithmType);

            //var tabStringHex = new string[16];
            var bytes = dataToHash.GetBytes(encoding);
            var result = hashAlgorithm.ComputeHash(bytes);
            var hexResult = new StringBuilder(result.Length);

            /*          
            var retStr = BitConverter.ToString(result);
            retStr = retStr.Replace("-", "").ToUpper();
            */
            foreach (var c in result)
                hexResult.Append(c.ToString("X2")); // Convert to hexadecimal
            return hexResult.ToString();
        }

        public static bool IsHashMatch(HashAlgorithmType hashAlgorithmType,
            string hashedText, string unhashedText, string salt, Encoding encoding = null)
        {
            salt.EnsureHasValue(nameof(salt));
            unhashedText = $"{unhashedText}@{salt}";
            return IsHashMatch(hashAlgorithmType, hashedText, unhashedText, encoding);
        }

        public static bool IsHashMatch(
            HashAlgorithmType hashAlgorithmType,
            string hashedText,
            string unhashedText,
            Encoding encoding = null)
        {
            hashedText.EnsureHasValue(nameof(hashedText));
            unhashedText.EnsureHasValue(nameof(unhashedText));

            var hashedTextToCompare = Hash(hashAlgorithmType, unhashedText, encoding);
            return string.Equals(hashedText, hashedTextToCompare, StringComparison.Ordinal);
        }

        #endregion

        #region 验签算法
        /// <summary>
        /// 根据参数产生签名
        /// </summary>
        public static string MakeSignature(string appId, string appKey, string timestamp, string nonce, Encoding encoding = null, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Sha1)
        {
            return MakeSignature(appId, appKey, timestamp, nonce, null, encoding, hashAlgorithmType);
        }

        /// <summary>
        /// 根据参数产生签名
        /// </summary>
        /// <remarks>encodingType = UTF8</remarks>
        public static string MakeSignature(string appId, string appKey, string timestamp, string nonce, NameValueCollection parameters, Encoding encoding = null, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Sha1)
        {
            appId.EnsureNotNull(name: nameof(appId));
            appKey.EnsureNotNull(name: nameof(appKey));
            timestamp.EnsureNotNull(name: nameof(timestamp));
            nonce.EnsureNotNull(name: nameof(nonce));

            var @params = new NameValueCollection { { nameof(appId), appId }, { nameof(timestamp), timestamp }, { nameof(nonce), nonce } };

            if (parameters == null)
                parameters = new NameValueCollection(3);
            parameters.Add(@params);

            return MakeSignature(parameters, appKey, encoding, hashAlgorithmType);
        }

        /// <summary>
        /// 构造微信支付签名
        /// </summary>
        public static string MakeSignature(NameValueCollection parameters, string appKey, Encoding encoding = null, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Sha1)
        {
            var keys = new List<string>(parameters.Keys.Count);
            keys.AddRange(from object key in parameters.Keys select key.ToString());
            keys.Sort();

            var sb = new StringBuilder();
            foreach (var key in keys)
            {
                string v = parameters[key];
                sb.Append($"{key}={v}&");
            }
            sb.Append($"appkey={appKey}");
            var sign = Hash(hashAlgorithmType, sb.ToString(), null, encoding);
            return sign;
        }

        /// <summary>
        /// 随机生成Noncestr
        /// </summary>
        /// <returns></returns>
        public static string GetNoncestr(Encoding encoding = null, HashAlgorithmType hashAlgorithmType = HashAlgorithmType.Md5)
        {
            return Hash(hashAlgorithmType, new Random().Next(1000).ToString(), encoding);
        }

        /// <exception cref="OverflowException"/>
        public static string GetTimestamp()
        {
            var ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        #endregion
    }
}
