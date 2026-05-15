using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography;

/// <summary>
/// 提供 RSA 非对称加密与数字签名 (支持标准 PEM 格式密钥)
/// </summary>
public static class RsaUtil
{
    /// <summary>
    /// 使用私钥生成 RSA2 (SHA256) 数字签名 (常用于支付宝/微信支付请求签名)
    /// </summary>
    /// <param name="data">待签名的原始字符串</param>
    /// <param name="privateKeyPem">标准的 PEM 格式私钥</param>
    /// <returns>Base64 格式的签名字符串</returns>
    public static string SignDataSha256(string data, string privateKeyPem, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var rsa = RSA.Create();

        // .NET 5+ 支持直接导入标准的 PEM 格式密钥
        rsa.ImportFromPem(privateKeyPem);

        var dataBytes = encoding.GetBytes(data);
        var signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signatureBytes);
    }

    /// <summary>
    /// 使用公钥验证 RSA2 (SHA256) 数字签名 (常用于验证第三方回调的合法性)
    /// </summary>
    /// <param name="data">原始字符串</param>
    /// <param name="signatureBase64">对方传来的 Base64 签名</param>
    /// <param name="publicKeyPem">标准的 PEM 格式公钥</param>
    public static bool VerifyDataSha256(string data, string signatureBase64, string publicKeyPem, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var rsa = RSA.Create();

        rsa.ImportFromPem(publicKeyPem);

        var dataBytes = encoding.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signatureBase64);

        return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// 公钥加密 (仅限短文本，如加密传输 AES 密钥或极短的敏感信息)
    /// </summary>
    public static string Encrypt(string plainText, string publicKeyPem, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        // OAEP SHA256 是目前最安全的填充模式
        var cipherBytes = rsa.Encrypt(encoding.GetBytes(plainText), RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 私钥解密
    /// </summary>
    public static string Decrypt(string cipherTextBase64, string privateKeyPem, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = rsa.Decrypt(cipherBytes, RSAEncryptionPadding.OaepSHA256);
        return encoding.GetString(plainBytes);
    }
}