/*
   // 1. 默认生成 32 位 ID
   var id1 = BatchIdGenerator.GenerateBatchId(); 
   // 输出: BATCH20231027103045123_001_abc123...
   
   // 2. 生成 16 位短 ID
   var id2 = BatchIdGenerator.GenerateBatchId(16); 
   // 输出: BATCH202310271030...
   
   // 3. 生成 64 位长 ID
   var id3 = BatchIdGenerator.GenerateBatchId(64, "ORDER"); 
   // 输出: ORDER20231027103045123_001_abc123...xyz789...
 */

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 提供基于时间戳和随机数的批次ID生成器（静态工具类）。
/// 优化点：
/// 1. 支持自定义 ID 长度，自动截取或填充随机部分。
/// 2. 使用 Interlocked 和 SpinWait 替代 lock，提升并发性能。
/// 3. 增加随机字符串长度，提升安全性。
/// 4. 严格处理时钟回拨，避免数据不一致。
/// 5. 优化随机字符串生成算法，使用 Base64 替代模运算。
/// 6. 使用静态字典缓存生成器实例，支持多前缀并发且互不干扰。
/// </summary>
public class BatchIdGenerator
{
    // 使用 ConcurrentDictionary 缓存不同 Prefix 对应的生成器实例
    private static readonly ConcurrentDictionary<string, GeneratorCore> _generators = new();

    // 使用 RandomNumberGenerator 生成安全的随机数
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    // 默认 ID 长度
    private const int DefaultLength = 32;

    /// <summary>
    /// 生成新的批次ID（默认前缀 "BATCH"，默认长度 32）
    /// </summary>
    public static string GenerateBatchId()
    {
        return GenerateBatchId(DefaultLength, "BATCH");
    }

    /// <summary>
    /// 生成指定长度的批次ID（默认前缀 "BATCH"）
    /// </summary>
    /// <param name="length">期望的ID总长度</param>
    public static string GenerateBatchId(int length)
    {
        return GenerateBatchId(length, "BATCH");
    }

    /// <summary>
    /// 生成指定前缀的新批次ID（默认长度 32）
    /// </summary>
    /// <param name="prefix">ID前缀</param>
    public static string GenerateBatchId(string prefix)
    {
        return GenerateBatchId(DefaultLength, prefix);
    }

    /// <summary>
    /// 生成指定前缀和长度的批次ID
    /// </summary>
    /// <param name="length">期望的ID总长度</param>
    /// <param name="prefix">ID前缀</param>
    public static string GenerateBatchId(int length, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("前缀不能为空。", nameof(prefix));

        // 获取或创建对应前缀的生成器核心实例
        var generator = _generators.GetOrAdd(prefix, p => new GeneratorCore(p));

        return generator.Generate(length);
    }

    /// <summary>
    /// 生成安全的随机字符串
    /// </summary>
    /// <param name="byteLength">原始字节长度（Base64编码后长度约为 byteLength * 1.3）</param>
    /// <returns>Base64 字符串（已移除末尾的 '=' 填充符）</returns>
    public static string GenerateSecureRandomString(int byteLength = 8)
    {
        // 1. 生成随机字节
        var bytes = new byte[byteLength];
        _rng.GetBytes(bytes);

        // 2. 直接转换为 Base64 字符串
        var base64 = Convert.ToBase64String(bytes);

        // 3. 移除末尾的填充符 '='，使其更紧凑
        return base64.TrimEnd('=');
    }

    /// <summary>
    /// 内部核心生成器类，持有状态（时间戳和序列号）
    /// </summary>
    private class GeneratorCore(string prefix)
    {
        private long _lastTimestamp;
        private int _sequence;

        // 固定部分：前缀 + 时间戳(17) + 下划线(1) + 序列号(3-4) + 下划线(1) ≈ 23-24 字符
        // 格式: PREFIXyyyyMMdd_HHmmss_fff_SEQ_Random
        private const int FixedPartLength = 24;

        public string Generate(int targetLength)
        {
            var timestamp = DateTime.UtcNow;

            if (timestamp.Kind == DateTimeKind.Unspecified)
            {
                throw new ArgumentException("时间类型必须指定为 Utc 或 Local。", nameof(timestamp));
            }

            var currentTimestamp = timestamp.Ticks;

            // 使用 SpinWait 处理高并发下的自旋等待
            var spin = new SpinWait();
            int sequence;

            while (true)
            {
                var originalTimestamp = _lastTimestamp;
                var originalSequence = _sequence;

                // 1. 严格处理时钟回拨
                if (currentTimestamp < originalTimestamp)
                {
                    throw new InvalidOperationException(
                        $"系统时钟发生回拨。当前时间: {new DateTime(currentTimestamp):yyyy-MM-dd HH:mm:ss.fff}, 上次记录时间: {new DateTime(originalTimestamp):yyyy-MM-dd HH:mm:ss.fff}。请检查服务器 NTP 同步设置。");
                }

                if (currentTimestamp == originalTimestamp)
                {
                    // 2. 同一毫秒内，序列号自增
                    var newSequence = (originalSequence + 1) % 1000;

                    // 使用 CAS (Compare-And-Swap) 原子性地更新序列号
                    if (Interlocked.CompareExchange(ref _sequence, newSequence, originalSequence) == originalSequence)
                    {
                        sequence = newSequence;
                        break;
                    }
                }
                else
                {
                    // 3. 新的一毫秒，重置序列号
                    if (Interlocked.CompareExchange(ref _lastTimestamp, currentTimestamp, originalTimestamp) == originalTimestamp)
                    {
                        Interlocked.Exchange(ref _sequence, 0);
                        sequence = 0;
                        break;
                    }
                }

                // 自旋等待，避免 CPU 空转
                spin.SpinOnce();
            }

            // 计算固定部分的长度
            // 格式: {Prefix}{yyyyMMdd_HHmmss_fff}_{seq}_
            var timeStr = timestamp.ToString("yyyyMMdd_HHmmss_fff");
            var seqStr = sequence.ToString("D3");
            var fixedPart = $"{prefix}{timeStr}_{seqStr}_";
            var fixedLen = fixedPart.Length;

            // 计算需要的随机部分长度
            // 如果目标长度小于固定部分长度，直接截断固定部分（虽然不推荐，但保证不报错）
            if (targetLength <= fixedLen)
            {
                return fixedPart.Substring(0, targetLength);
            }

            var randomPartLen = targetLength - fixedLen;

            // 估算需要的字节数：Base64 长度 ≈ 字节数 * 1.33
            // 我们多生成一点，然后截取，确保长度足够
            var bytesNeeded = (int)Math.Ceiling(randomPartLen / 4.0 * 3.0);

            // 生成随机字符串
            var randomStr = GenerateSecureRandomString(bytesNeeded);

            // 拼接并截取到目标长度
            return $"{fixedPart}{randomStr}".Substring(0, targetLength);
        }
    }
}
