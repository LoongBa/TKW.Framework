#nullable enable
using System;
using System.Security.Cryptography;
using System.Threading;

namespace TKW.Framework.Tools.IdGenerator;

/// <summary>
/// 分布式 ID 生成器 (强一致性)。
/// 规则：[前缀] + [yyyyMMddHHmmss] + [WorkerId(2位)] + [毫秒序列(3位)] + [随机字符串]
/// 适用：Kubernetes 多副本部署、要求绝对防止碰撞的核心业务流水号。
/// </summary>
public class DistributedIdGenerator : IIdGenerator
{
    private long _LastTimestamp = -1L;
    private int _Sequence;
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    // 工作节点标识
    private readonly string _WorkerIdString;

    /// <summary>
    /// 实例化分布式 ID 生成器
    /// </summary>
    /// <param name="workerId">机器/工作节点 ID (0-99)，同一集群内不同实例必须分配不同的值</param>
    public DistributedIdGenerator(int workerId)
    {
        if (workerId < 0 || workerId > 99)
            throw new ArgumentOutOfRangeException(nameof(workerId), "WorkerId 必须在 0 到 99 之间");

        _WorkerIdString = workerId.ToString("D2");
    }

    public string NewId(int length = 32, string? prefix = null)
    {
        var timestamp = GetTimestamp();
        int seq;

        var spin = new SpinWait();
        while (true)
        {
            var last = Interlocked.Read(ref _LastTimestamp);
            if (timestamp < last)
                throw new InvalidOperationException($"时钟回拨异常。当前时间: {timestamp}, 上次时间: {last}");

            if (timestamp == last)
            {
                var nextSeq = Interlocked.Increment(ref _Sequence) % 1000;
                if (nextSeq == 0)
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

        var timePart = DateTime.Now.ToString("yyyyMMddHHmmss");

        // 核心区别：拼装时加入 WorkerId
        var baseId = $"{prefix}{timePart}{_WorkerIdString}{seq:D3}";

        if (baseId.Length >= length) return baseId[..length];

        var randomLen = length - baseId.Length;
        return baseId + RandomNumberGenerator.GetString(Alphabet, randomLen);
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetTimestamp();
        while (timestamp <= lastTimestamp) timestamp = GetTimestamp();
        return timestamp;
    }
}