using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography
{
    public static class StringUtilities
    {
        #region 5.0 生成随机字符串 + static string GetRandomString(int length, bool useNum, bool useLow, bool useUpp, bool useSpe, string preFix)

        /// <summary>
        /// 产生16位长度的随机字符串
        /// </summary>
        public static string Generate16RandomString()
        {
            return GetRandomString(16, true, true, true, false); //8位随机字符串
        }
        /// <summary>
        /// 产生8位长度的随机字符串
        /// </summary>
        public static string Generate8RandomString()
        {
            return GetRandomString(8, true, true, true, false); //8位随机字符串
        }

        ///<summary>
        ///生成随机字符串 
        ///</summary>
        ///<param name="length">目标字符串的长度</param>
        ///<param name="useNum">是否包含数字，1=包含，默认为包含</param>
        ///<param name="useLow">是否包含小写字母，1=包含，默认为包含</param>
        ///<param name="useUpp">是否包含大写字母，1=包含，默认为包含</param>
        ///<param name="useSpe">是否包含特殊字符，1=包含，默认为不包含</param>
        ///<param name="preFix">要包含的自定义字符，直接输入要包含的字符列表</param>
        /// <remarks>方法来自：https://blog.csdn.net/weixin_43118159/article/details/120827296 略改</remarks>
        /// <remarks>该方法有待改进，临时使用</remarks>
        ///<returns>指定长度的随机字符串</returns>
        public static string GetRandomString(int length, bool useNum, bool useLow, bool useUpp, bool useSpe, string preFix = null)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Create().GetBytes(bytes);
            var random = new Random(BitConverter.ToInt32(bytes, 0));
            var result = string.Empty;
            var text = preFix ?? string.Empty;
            if (useNum) { text += "0123456789"; }
            if (useLow) { text += "abcdefghijklmnopqrstuvwxyz"; }
            if (useUpp) { text += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; }
            if (useSpe) { text += "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"; }
            //这里改为用 LinQ 会更好
            for (var i = 0; i < length; i++)
                result += text.Substring(random.Next(0, text.Length - 1), 1);
            return result;
        }
        #endregion

        #region 生成随机密码

        private static readonly char[] Punctuations = "!@#$%^&*()_-+=[{]};:>|./?".ToCharArray();
        private static readonly char[] StartingChars = { '<', '&' };

        /// <summary>生成指定长度的随机密码。</summary>
        /// <returns>指定长度的随机密码。</returns>
        /// <param name="length">生成的密码的字符数。长度必须介于 1 和 128 个字符之间。</param>
        /// <param name="numberOfNonAlphanumericCharacters">生成的密码中非字母数字字符的最小数量 （如 @、#、！、%、&amp; 等) 。</param>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="length" /> 小于 1 或大于 128 - 或 -<paramref name="numberOfNonAlphanumericCharacters" /> 小于 0 或大于 <paramref name="length" />。</exception>
        [Obsolete("Obsolete")]
        public static string GeneratePassword(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 1
                || length > 128)
                throw new ArgumentException("Membership_password_length_incorrect");
            if (numberOfNonAlphanumericCharacters > length
                || numberOfNonAlphanumericCharacters < 0)
                throw new ArgumentException(
                    "Membership_min_required_non_alphanumeric_characters_incorrect:numberOfNonAlphanumericCharacters");
            string s;
            do
            {
                var data = new byte[length];
                var chArray = new char[length];
                var num1 = 0;
                new RNGCryptoServiceProvider().GetBytes(data);
                for (var index = 0; index < length; ++index)
                {
                    var num2 = data[index] % 87;
                    if (num2 < 10)
                        chArray[index] = (char)(48 + num2);
                    else if (num2 < 36)
                        chArray[index] = (char)(65 + num2 - 10);
                    else if (num2 < 62)
                    {
                        chArray[index] = (char)(97 + num2 - 36);
                    }
                    else
                    {
                        chArray[index] = Punctuations[num2 - 62];
                        ++num1;
                    }
                }
                if (num1 < numberOfNonAlphanumericCharacters)
                {
                    var random = new Random();
                    for (var index1 = 0;
                         index1 < numberOfNonAlphanumericCharacters - num1;
                         ++index1)
                    {
                        int index2;
                        do
                        {
                            index2 = random.Next(0, length);
                        } while (!char.IsLetterOrDigit(chArray[index2]));
                        chArray[index2] = Punctuations[random.Next(0, Punctuations.Length)];
                    }
                }
                s = new string(chArray);
            } while (IsDangerousString(s, out _));
            return s;
        }

        private static bool IsDangerousString(string s, out int matchIndex)
        {
            matchIndex = 0;
            var startIndex = 0;
            while (true)
            {
                var index = s.IndexOfAny(StartingChars, startIndex);
                if (index >= 0
                    && index != s.Length - 1)
                {
                    matchIndex = index;
                    switch (s[index])
                    {
                        case '&':
                            if (s[index + 1] != 35)
                                break;
                            goto label_7;
                        case '<':
                            if (IsAtoZ(s[index + 1])
                                || s[index + 1] == 33
                                || (s[index + 1] == 47 || s[index + 1] == 63))
                                goto label_5;
                            break;
                    }
                    startIndex = index + 1;
                }
                else
                    break;
            }
            return false;
        label_5:
            return true;
        label_7:
            return true;
        }

        private static bool IsAtoZ(char c)
        {
            if (c >= 97 && c <= 122)
                return true;
            if (c >= 65)
                return c <= 90;
            return false;
        }

        #endregion


        /*
             *   gb2312 80
             * 
             * 　　01-09区为特殊符号。 　　
             * 　　16-55区为一级汉字，按拼音排序。 　
             * 　　56-87区为二级汉字，按部首/笔画排序。 　　
             * 　　10-15区及88-94区则未有编码。 
         * 　　
         * 所有数字都是从 1 开始
             * 　　
             * 每个汉字及符号以两个字节来表示。
             * 第一个字节称为“高位字节”（也称“区字节）”，upper byte
             * 第二个字节称为“低位字节”（也称“位字节”）。low byte　　
             * “高位字节”使用了0xA1-0xF7(把01-87区的区号加上0xA0)，
             * “低位字节”使用了0xA1-0xFE(把01-94加上 0xA0)。 
             * 由于一级汉字从16区起始，汉字区的“高位字节”的范围是0xB0-0xF7，
             * “低位字节”的范围是0xA1-0xFE，
             * 占用的码位是 72*94=6768。
             * 其中有5个空位是D7FA-D7FE(55区90-94)
                */

        #region 生成中文字符

        public static string GetRandomPopularSimplifiedChinese(this int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            const byte minUpper = 16;
            const byte maxUpper = 55;
            const byte rangeLow = 94;
            var addParamer = 0xA0;

            var tempStr = new StringBuilder();
            var tempUpper = maxUpper - minUpper + 1;
            var gb = Encoding.GetEncoding("gb2312");

            for (var i = 0; i < length; i++)
            {
                var rdUpperValue = Next(tempUpper) + minUpper;
                int rdLowValue;
                do
                {
                    rdLowValue = Next(rangeLow) + 1;//索引从1开始，所以94种子生成的随机数+1
                }
                while (rdUpperValue == maxUpper && rdLowValue >= 90);//D7FA-D7FE是空位(55区90-94)

                rdUpperValue += addParamer;
                rdLowValue += addParamer;

                var byteArray = new[] { (byte)rdUpperValue, (byte)rdLowValue };

                tempStr.Append(gb.GetString(byteArray));
            }

            return tempStr.ToString();
        }

        /// <summary>
        /// 生成小于输入值绝对值的随机数
        /// </summary>
        /// <param name="numSeeds"></param>
        /// <returns></returns>
        public static int Next(this int numSeeds)
        {
            numSeeds = Math.Abs(numSeeds);
            if (numSeeds <= 1)
            {
                return 0;
            }

            var length = 4;
            if (numSeeds <= byte.MaxValue)
            {
                length = 1;
            }
            else if (numSeeds <= short.MaxValue)
            {
                length = 2;
            }

            return Next(numSeeds, length);
        }
        private static int Next(int numSeeds, int length)
        {
            // Create a byte array to hold the random value.
            var buffer = new byte[length];
            // // Create a new instance of the RNGCryptoServiceProvider.
            // var gen = new RNGCryptoServiceProvider();
            // // Fill the array with a random value.
            // gen.GetBytes(buffer);

            // Use RandomNumberGenerator instead of RNGCryptoServiceProvider.
            RandomNumberGenerator.Fill(buffer);

            // Convert the byte to an uint value to make the modulus operation easier.
            uint randomResult = 0x0;//这里用uint作为生成的随机数
            for (var i = 0; i < length; i++)
            {
                randomResult |= ((uint)buffer[i] << ((length - 1 - i) * 8));
            }
            // Return the random number mod the number
            // of sides.  The possible values are zero-based
            return (int)(randomResult % numSeeds);
        }

        #endregion
    }
}