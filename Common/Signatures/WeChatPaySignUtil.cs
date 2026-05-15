using System.Collections.Generic;
using System.Text;
using TKW.Framework.Cryptography;

namespace TKW.Framework.Signatures
{
    public static class WeChatPaySignUtil
    {
        /// <summary>
        /// 构造微信支付 V2 API 签名
        /// </summary>
        /// <param name="parameters">待签名参数字典</param>
        /// <param name="apiKey">商户 API 密钥</param>
        /// <param name="useHmacSha256">是否使用 HMAC-SHA256 (推荐 true，老旧接口用 MD5 传 false)</param>
        public static string BuildSignature(IDictionary<string, string> parameters, string apiKey, bool useHmacSha256 = true)
        {
            // SortedDictionary 自动按 Key 的 ASCII 字典序排列
            var sortedParams = new SortedDictionary<string, string>(parameters);
            var sb = new StringBuilder();

            foreach (var kvp in sortedParams)
            {
                // 微信规则：参数值为空或参数名为 sign 时，不参与签名
                if (!string.IsNullOrEmpty(kvp.Value) && kvp.Key != "sign")
                {
                    sb.Append($"{kvp.Key}={kvp.Value}&");
                }
            }

            // 拼接 API 密钥
            sb.Append($"key={apiKey}");
            var rawString = sb.ToString();

            // 计算哈希并转大写
            var sign = useHmacSha256
                ? HmacUtil.ComputeHmacSha256(rawString, apiKey)
                : HashUtil.ComputeMd5(rawString);

            return sign.ToUpperInvariant();
        }
    }
}