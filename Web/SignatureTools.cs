using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Web;

public static class SignatureTools
{
    /// <summary>
    ///     根据参数产生签名
    /// </summary>
    public static string MakeSignature(string appId, string appKey, string timestamp, string nonce)
    {
        return MakeSignature(appId, appKey, timestamp, nonce, null, Encoding.UTF8);
    }

    /// <summary>
    ///     根据参数产生签名
    /// </summary>
    public static string MakeSignature(string appId, string appKey, string timestamp, string nonce,
        NameValueCollection parameters)
    {
        return MakeSignature(appId, appKey, timestamp, nonce, parameters, Encoding.UTF8);
    }

    /// <summary>
    ///     根据参数产生签名
    /// </summary>
    /// <remarks>encodingType = UTF8</remarks>
    public static string MakeSignature(string appId, string appKey, string timestamp, string nonce,
        NameValueCollection parameters, Encoding encodingType)
    {
        appId = appId.EnsureHasValue().TrimSelf();
        appKey = appKey.EnsureHasValue().TrimSelf();
        timestamp = timestamp.EnsureHasValue().TrimSelf();
        nonce = nonce.EnsureHasValue().TrimSelf();

        var dictionary = new System.Collections.Generic.SortedDictionary<string, string>
        {
            { "appId", appId.Trim() },
            { "timestamp", timestamp.Trim() },
            { "nonce", nonce.Trim() }
        };

        if (parameters != null)
            foreach (var key in parameters.AllKeys)
                dictionary[key!.ToLower()] = parameters[key];

        var sb = new StringBuilder();
        foreach (var kvp in dictionary) sb.AppendFormat("{0}={1}&", kvp.Key, kvp.Value);
        sb.AppendFormat("appkey={0}", appKey);

        using var sha1 = SHA1.Create();
        var encoding = encodingType ?? Encoding.UTF8;
        var bytes = encoding.GetBytes(sb.ToString());
        bytes = sha1.ComputeHash(bytes);

        var hashString = BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();

        return hashString;
    }


    /// <summary>
    ///     构造微信支付签名
    /// </summary>
    public static string MakeTenPaySignature(Hashtable parameters, string appKey, Encoding encodingType = null)
    {
        var keys = new ArrayList(parameters.Keys);
        keys.Sort();

        var sb = new StringBuilder();
        foreach (var key in keys)
        {
            var v = (string)parameters[key];
            sb.Append($"{key}={v}&");
        }

        sb.Append($"appkey={appKey}");
        var sign = GetMd5(sb.ToString(), encodingType);
        return sign;
    }

    /// <summary>
    ///     随机生成 Noncestr
    /// </summary>
    /// <returns></returns>
    public static string GetNoncestr(Encoding encodingType = null)
    {
        var random = new Random();
        return GetMd5(random.Next(1000).ToString(), encodingType);
    }

    /// <exception cref="OverflowException" />
    public static string GetTimestamp()
    {
        var ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds).ToString();
    }

    /// <exception cref="InvalidOperationException">未使用符合 FIPS 的算法策略。</exception>
    public static string GetMd5(string encryptStr, Encoding encoding = null)
    {
        encryptStr.EnsureHasValue().TrimSelf();
        encoding ??= Encoding.UTF8;

        using var md5 = MD5.Create();
        byte[] inputBytes;

        // 使用指定的编码方式将字符串转换为字节数组
        try
        {
            inputBytes = encoding.GetBytes(encryptStr);
        }
        catch (Exception)
        {
            inputBytes = Encoding.UTF8.GetBytes(encryptStr);
        }

        var hashBytes = md5.ComputeHash(inputBytes);

        var retStr = BitConverter.ToString(hashBytes);
        retStr = retStr.Replace("-", "").ToUpper(CultureInfo.CurrentCulture);
        return retStr;
    }
}