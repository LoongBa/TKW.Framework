using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography
{
    /// <summary>
    /// MD5 加密字符串，支持盐值加密，不可逆
    /// 
    /// Written by elemeNtttt 2015-12-7
    /// </summary>
    public static class Md5Helper
    {
        public static string GetRandomSalt()
        {
            var strSep = ",";
            var chrSep = strSep.ToCharArray();

            var strChar = "1,2,3,4,5,6,7,8,9,0,A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z";
            var aryChar = strChar.Split(chrSep, strChar.Length);

            var strRandom = string.Empty;
            var rnd = new Random();

            var num = new Random();
            var count = num.Next(5, 10);
            //生成随机字符串
            for (var i = 0; i < count; i++)
            {
                strRandom += aryChar[rnd.Next(15)];
            }
            return strRandom;
        }

        /// <summary>
        /// MD5 加密字符串
        /// </summary>
        /// <param name="rawPass">源字符串</param>
        /// <returns>加密后字符串</returns>
        public static string Md5Encoding(string rawPass)
        {
            // 创建MD5类的默认实例：MD5CryptoServiceProvider
            var md5 = MD5.Create();
            var bs = Encoding.UTF8.GetBytes(rawPass);
            var hs = md5.ComputeHash(bs);
            var sb = new StringBuilder();
            foreach (var b in hs)
            {
                // 以十六进制格式格式化
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// MD5盐值加密
        /// </summary>
        /// <param name="rawPass">源字符串</param>
        /// <param name="salt">盐值</param>
        /// <returns>加密后字符串</returns>
        public static string Md5Encoding(string rawPass, object salt)
        {
            return salt == null ? rawPass : Md5Encoding(rawPass + "{" + salt + "}");
        }
    }
}
