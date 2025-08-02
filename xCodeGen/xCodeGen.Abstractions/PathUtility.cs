using System;
using System.IO;

namespace xCodeGen.Abstractions
{
    /// <summary>
    /// 跨框架路径处理工具类
    /// 兼容 .NET Framework、.NET Core 及 .NET Standard
    /// </summary>
    public static class PathUtility
    {
        /// <summary>
        /// 计算两个路径之间的相对路径
        /// 替代 .NET Core 2.1+ 中的 Path.GetRelativePath 方法，兼容低版本框架
        /// </summary>
        /// <param name="relativeTo">基准路径（相对于此路径）</param>
        /// <param name="path">目标路径</param>
        /// <returns>从 relativeTo 到 path 的相对路径</returns>
        /// <exception cref="ArgumentNullException">当输入路径为 null 时抛出</exception>
        /// <exception cref="ArgumentException">当输入路径不是绝对路径时抛出</exception>
        public static string GetRelativePath(string relativeTo, string path)
        {
            if (string.IsNullOrEmpty(relativeTo))
                throw new ArgumentNullException(nameof(relativeTo));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            // 验证路径是否为绝对路径
            if (!Path.IsPathRooted(relativeTo))
                throw new ArgumentException("基准路径必须是绝对路径", nameof(relativeTo));

            if (!Path.IsPathRooted(path))
                throw new ArgumentException("目标路径必须是绝对路径", nameof(path));

            // 统一路径分隔符为 '/'（URI 兼容格式）
            string normalizedRelativeTo = relativeTo.Replace(Path.DirectorySeparatorChar, '/')
                                                   .Replace(Path.AltDirectorySeparatorChar, '/');
            string normalizedPath = path.Replace(Path.DirectorySeparatorChar, '/')
                                       .Replace(Path.AltDirectorySeparatorChar, '/');

            // 确保路径以 '/' 结尾（处理目录场景）
            if (!normalizedRelativeTo.EndsWith("/"))
                normalizedRelativeTo += "/";

            if (!normalizedPath.EndsWith("/"))
                normalizedPath += "/";

            // 使用 URI 计算相对路径
            var baseUri = new Uri(normalizedRelativeTo);
            var targetUri = new Uri(normalizedPath);
            Uri relativeUri = baseUri.MakeRelativeUri(targetUri);

            // 转换为字符串并替换为当前系统的路径分隔符
            string relativePath = relativeUri.ToString()
                                             .Replace('/', Path.DirectorySeparatorChar)
                                             .TrimEnd(Path.DirectorySeparatorChar);

            // 处理根路径相同但子路径不同的特殊情况
            return string.IsNullOrEmpty(relativePath) ? "." : relativePath;
        }

        /// <summary>
        /// 标准化路径格式（统一分隔符并移除尾部斜杠）
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                       .TrimEnd(Path.DirectorySeparatorChar);
        }
    }
}
