#nullable enable
namespace TKW.Framework.Common.Tools;

/// <summary>
/// 生成器接口，定义了生成 ID 的方法。
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// 产生新的 ID，默认长度为 32，默认无前缀。
    /// 生成规则：前缀 + 时间戳（精确到秒） + 3 位毫秒内序列 + 随机字符串（长度根据总长度自动调整）。
    /// 例如：BATCH20231027103045123_001_abc123...xyz789
    /// </summary>
    /// <param name="length">总长度</param>
    /// <param name="prefix">前缀</param>
    string NewId(int length = 32, string? prefix = null);
}