#nullable enable
using System;
using System.Security.Cryptography;
using System.Threading;

namespace TKW.Framework.Common.Tools;

public class DefaultIdGenerator : IIdGenerator
{
    private long _LastTimestamp = -1L;
    private int _Sequence;
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    ///<inheritdoc/>
    /// <exception cref="InvalidOperationException"></exception>
    public string NewId(int length = 32, string? prefix = null)
    {
        var timestamp = GetTimestamp();
        var seq = 0;

        // 使用 CAS 保证线程安全并处理时钟回拨
        var spin = new SpinWait();
        while (true)
        {
            var last = Interlocked.Read(ref _LastTimestamp);
            if (timestamp < last)
                throw new InvalidOperationException("时钟回拨异常");

            if (timestamp == last)
            {
                var nextSeq = Interlocked.Increment(ref _Sequence) % 1000;
                if (nextSeq == 0) // 当前毫秒序列溢出，自旋等待下一毫秒
                {
                    timestamp = WaitNextMillis(last);
                    continue;
                }
                seq = nextSeq;
                break;
            }
            if (Interlocked.CompareExchange(ref _LastTimestamp, timestamp, last) == last)
            {
                Interlocked.Exchange(ref _Sequence, 0);
                seq = 0;
                break;
            }
            spin.SpinOnce();
        }

        // 拼接逻辑
        var timePart = DateTime.Now.ToString("yyyyMMddHHmmss");
        var baseId = $"{prefix}{timePart}{seq:D3}";

        if (baseId.Length >= length) return baseId[..length];

        // 填充随机位
        var randomLen = length - baseId.Length;
        return baseId + GenerateRandomString(randomLen);
    }

    private long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetTimestamp();
        while (timestamp <= lastTimestamp) timestamp = GetTimestamp();
        return timestamp;
    }

    private string GenerateRandomString(int length)
    {
        Span<char> result = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(result);
    }

    #region 业务便捷方法 (不在接口中)
    public string GenerateBatchId() => NewId(32, "BATCH");
    public string GenerateOrderNo() => NewId(24, "ORD");
    #endregion
}