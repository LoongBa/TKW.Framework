using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Cryptography;

/// <summary>
/// 提供基于哈希的消息认证码 (HMAC) 计算
/// </summary>
public static class HmacUtil
{
    /// <summary>
    /// 计算 HMAC-SHA256 摘要 (现代 API 验签最常用)
    /// </summary>
    public static string ComputeHmacSha256(string data, string key, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(key);

        encoding ??= Encoding.UTF8;

        // .NET 8+ 原生静态方法，避免了 new HMACSHA256() 的对象分配开销
        var hashBytes = HMACSHA256.HashData(encoding.GetBytes(key), encoding.GetBytes(data));

        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 验证 HMAC 是否匹配 (防时序攻击)
    /// </summary>
    public static bool VerifyHmacSha256(string data, string key, string expectedHexHash, Encoding? encoding = null)
    {
        var actualHash = ComputeHmacSha256(data, key, encoding);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actualHash),
            Convert.FromHexString(expectedHexHash));
    }
}