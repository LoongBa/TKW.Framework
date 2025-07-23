using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Core.Utilities
{
    /// <summary>
    /// 类型处理工具
    /// </summary>
    public class TypeUtility
    {
        private static readonly Dictionary<string, string> _typeAliases = new Dictionary<string, string>
        {
            { "System.String", "string" },
            { "System.Int32", "int" },
            { "System.Int64", "long" },
            { "System.Boolean", "bool" },
            { "System.Single", "float" },
            { "System.Double", "double" },
            { "System.Decimal", "decimal" },
            { "System.Object", "object" },
            { "System.Void", "void" }
        };

        /// <summary>
        /// 获取简化的类型名称
        /// </summary>
        public string SimplifyTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return fullTypeName;

            // 处理可空类型
            if (fullTypeName.StartsWith("System.Nullable`1["))
            {
                string underlyingType = fullTypeName.Substring("System.Nullable`1[".Length);
                underlyingType = underlyingType.TrimEnd(']');
                return $"{SimplifyTypeName(underlyingType)}?";
            }

            // 处理泛型类型
            if (fullTypeName.Contains('`'))
            {
                int backtickIndex = fullTypeName.IndexOf('`');
                string typeName = fullTypeName.Substring(0, backtickIndex);
                string genericPart = fullTypeName.Substring(backtickIndex + 2).TrimEnd(']');

                string simplifiedTypeName = SimplifyTypeName(typeName);
                IEnumerable<string> genericArgs = genericPart.Split(',')
                    .Select(t => SimplifyTypeName(t.Trim()));

                return $"{simplifiedTypeName}<{string.Join(", ", genericArgs)}>";
            }

            // 处理数组
            if (fullTypeName.EndsWith("[]"))
            {
                string elementType = fullTypeName.Substring(0, fullTypeName.Length - 2);
                return $"{SimplifyTypeName(elementType)}[]";
            }

            // 查找类型别名
            if (_typeAliases.TryGetValue(fullTypeName, out string alias))
                return alias;

            // 提取类型名称（去掉命名空间）
            int lastDotIndex = fullTypeName.LastIndexOf('.');
            if (lastDotIndex > 0 && lastDotIndex < fullTypeName.Length - 1)
                return fullTypeName.Substring(lastDotIndex + 1);

            return fullTypeName;
        }

        /// <summary>
        /// 判断是否为数值类型
        /// </summary>
        public static bool IsNumericType(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return false;

            // 处理可空数值类型
            if (typeFullName.StartsWith("System.Nullable`1["))
            {
                string underlyingType = typeFullName.Substring("System.Nullable`1[".Length).TrimEnd(']');
                return IsNumericType(underlyingType);
            }

            var numericTypes = new HashSet<string>
            {
                "System.Int32", "System.Int64", "System.Int16",
                "System.UInt32", "System.UInt64", "System.UInt16",
                "System.Single", "System.Double", "System.Decimal",
                "System.Byte", "System.SByte"
            };

            return numericTypes.Contains(typeFullName);
        }

        /// <summary>
        /// 判断是否为字符串类型
        /// </summary>
        public static bool IsStringType(string typeFullName)
        {
            return typeFullName == "System.String";
        }

        /// <summary>
        /// 判断是否为日期时间类型
        /// </summary>
        public bool IsDateTimeType(string typeFullName)
        {
            return typeFullName == "System.DateTime" || 
                   typeFullName == "System.DateTimeOffset";
        }
    }
}
    