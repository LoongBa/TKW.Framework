using System.Linq;
using System.Text.RegularExpressions;

namespace xCodeGen.Utilities
{
    /// <summary>
    /// 命名转换工具
    /// </summary>
    public class NamingUtility
    {
        /// <summary>
        /// 转换为帕斯卡命名法（首字母大写）
        /// </summary>
        public string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // 处理下划线分隔的命名
            if (name.Contains("_"))
            {
                return string.Join("", name.Split('_')
                    .Where(part => !string.IsNullOrEmpty(part))
                    .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));
            }

            // 处理骆驼命名法转帕斯卡
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// 转换为骆驼命名法（首字母小写）
        /// </summary>
        public string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // 处理下划线分隔的命名
            if (name.Contains("_"))
            {
                string pascal = ToPascalCase(name);
                return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
            }

            // 处理帕斯卡命名法转骆驼
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// 生成产物名称
        /// </summary>
        public string GenerateArtifactName(string className, string methodName, string artifactType)
        {
            return $"{className}{ToPascalCase(methodName)}{artifactType}";
        }

        /// <summary>
        /// 清理名称中的非法字符
        /// </summary>
        public string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // 替换非字母数字的字符
            return Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
        }
    }
}
    