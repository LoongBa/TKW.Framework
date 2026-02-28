using System;
using System.Text;

namespace xCodeGen.Core;

public static class MaskHelper
{
    /// <summary>
    /// 根据模式掩盖字符串。
    /// ?：保留原字符
    /// #：掩盖该位置字符
    /// 其它：保留模式中的字面量（如 @, .）
    /// </summary>
    public static string GetMaskedValue(string value, string pattern)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return value;

        var sb = new StringBuilder();
        int vIdx = 0;

        for (int pIdx = 0; pIdx < pattern.Length; pIdx++)
        {
            if (vIdx >= value.Length) break;
            char pChar = pattern[pIdx];

            // 处理转义
            if (pChar == '\\' && pIdx + 1 < pattern.Length)
            {
                sb.Append(pattern[++pIdx]); vIdx++; continue;
            }

            // 检查贪婪位
            bool isGreedy = (pIdx + 1 < pattern.Length && pattern[pIdx + 1] == '*');

            if (pChar == '?')
            {
                if (!isGreedy) sb.Append(value[vIdx++]);
                else
                {
                    // 贪婪保留：保留剩余长度减去模式后缀长度的所有字符
                    int suffixLen = CountPatternSuffix(pattern, pIdx + 2);
                    int count = Math.Max(0, value.Length - vIdx - suffixLen);
                    while (count-- > 0) sb.Append(value[vIdx++]);
                    pIdx++; // 跳过 *
                }
            }
            else if (pChar == '#')
            {
                if (!isGreedy) { sb.Append('*'); vIdx++; }
                else
                {
                    int suffixLen = CountPatternSuffix(pattern, pIdx + 2);
                    int count = Math.Max(0, value.Length - vIdx - suffixLen);
                    while (count-- > 0) { sb.Append('*'); vIdx++; }
                    pIdx++; // 跳过 *
                }
            }
            else
            {
                sb.Append(pChar);
                vIdx++;
            }
        }
        return sb.ToString();
    }
    private static int CountPatternSuffix(string pattern, int start)
        => pattern.Substring(start).Replace("*", "").Length; // 简易计算后缀所需的物理位数
}