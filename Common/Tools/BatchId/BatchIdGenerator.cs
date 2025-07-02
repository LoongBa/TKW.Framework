using System;
using System.Security.Cryptography;
using System.Text;

namespace TKW.Framework.Common.Tools.BatchId;

/// <summary>
/// 批次ID生成器（线程安全，支持时钟回拨处理）
/// </summary>
public class BatchIdGenerator(string prefix = "")
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private const string _chars = "abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly string _prefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}_";
    private DateTime _lastTimestamp = DateTime.MinValue;
    private int _sequence = 0;
    private readonly object _lock = new();

    /// <summary>
    /// 生成批次ID（格式：[前缀_]yyyyMMdd_HHmmss_fff_序列号_随机字符串）
    /// </summary>
    public string GenerateBatchId() => GenerateBatchId(DateTime.Now);

    /// <summary>
    /// 使用指定时间生成批次ID（用于历史数据处理）
    /// </summary>
    public string GenerateBatchId(DateTime timestamp)
    {
        int sequence;

        lock (_lock)
        {
            // 处理时钟回拨
            if (timestamp < _lastTimestamp)
            {
                // 轻微回拨（<500ms）使用虚拟时间
                if (_lastTimestamp - timestamp < TimeSpan.FromMilliseconds(500))
                {
                    timestamp = _lastTimestamp.AddMilliseconds(1);
                }
                else
                {
                    throw new InvalidOperationException($"系统时钟回拨过大: {_lastTimestamp - timestamp}");
                }
            }

            if (timestamp == _lastTimestamp)
            {
                // 同一毫秒内，序列号递增
                _sequence = (_sequence + 1) % 1000; // 限制序列号为0-999
            }
            else
            {
                // 新的时间，重置序列号
                _lastTimestamp = timestamp;
                _sequence = 0;
            }

            sequence = _sequence;
        }

        // 组合ID: 前缀 + 时间戳 + 序列号 + 随机字符串
        return $"{_prefix}{timestamp:yyyyMMdd_HHmmss_fff}_{sequence:D3}_{GenerateSecureRandomString(5)}";
    }

    private string GenerateSecureRandomString(int length)
    {
        var bytes = new byte[length];
        _rng.GetBytes(bytes);

        var sb = new StringBuilder(length);
        foreach (var b in bytes)
        {
            sb.Append(_chars[b % _chars.Length]);
        }

        return sb.ToString();
    }
}