using System;
using System.Linq;

namespace xCodeGen.Abstractions
{
    public static class MdHelper
    {
        /// <summary>
        /// 清理字符串以适配 Markdown 表格单元格或单行显示
        /// </summary>
        public static string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            return input
                .Replace("<", "&lt;")     // 处理泛型尖括号
                .Replace(">", "&gt;")
                .Replace("\r\n", "<br/>") // 表格内折行转为 HTML 换行
                .Replace("\n", "<br/>")
                .Replace("|", "\\|")      // 转义表格分隔符
                .Trim();
        }

        /// <summary>
        /// 处理 Blockquote 引用块中的多行内容
        /// </summary>
        public static string ToQuote(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            // Blockquote 每一行都必须以 > 开头
            var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return string.Join("\n> ", lines.Select(l => l.Trim()));
        }
    }
}