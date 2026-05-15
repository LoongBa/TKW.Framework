using System.Collections.Generic;
using System.Text;
using TKW.Framework.Cryptography;

namespace TKW.Framework.Signatures
{
    public static class AlipaySignUtil
    {
        /// <summary>
        /// 生成支付宝请求签名 (RSA2)
        /// </summary>
        /// <param name="parameters">所有请求参数</param>
        /// <param name="merchantPrivateKeyPem">商户的 RSA 私钥 (PEM格式)</param>
        public static string BuildSignature(IDictionary<string, string> parameters, string merchantPrivateKeyPem)
        {
            var rawString = BuildAlipayRawString(parameters);
            // 支付宝要求对排序后的字符串进行 RSA2 (SHA256) 签名，并返回 Base64
            return RsaUtil.SignDataSha256(rawString, merchantPrivateKeyPem);
        }

        /// <summary>
        /// 验证支付宝异步通知回调的签名 (RSA2)
        /// </summary>
        /// <param name="parameters">支付宝 POST 过来的所有表单参数</param>
        /// <param name="alipayPublicKeyPem">支付宝公钥 (PEM格式)</param>
        public static bool VerifyCallbackSignature(IDictionary<string, string> parameters, string alipayPublicKeyPem)
        {
            if (!parameters.TryGetValue("sign", out var signBase64))
            {
                return false;
            }

            var rawString = BuildAlipayRawString(parameters);
            return RsaUtil.VerifyDataSha256(rawString, signBase64, alipayPublicKeyPem);
        }

        /// <summary>
        /// 提取并拼接支付宝排序字符串规则
        /// </summary>
        private static string BuildAlipayRawString(IDictionary<string, string> parameters)
        {
            var sortedParams = new SortedDictionary<string, string>(parameters);
            var sb = new StringBuilder();

            foreach (var kvp in sortedParams)
            {
                // 支付宝规则：不包含 sign 字段，且 sign_type 虽然参与传递但某些历史版本有特殊处理，标准 V2 接口中 sign_type 要参与排序签名
                if (!string.IsNullOrEmpty(kvp.Value) && kvp.Key != "sign")
                {
                    sb.Append($"{kvp.Key}={kvp.Value}&");
                }
            }

            // 移除最后一个 '&'
            if (sb.Length > 0)
            {
                sb.Length--;
            }

            return sb.ToString();
        }
    }
}