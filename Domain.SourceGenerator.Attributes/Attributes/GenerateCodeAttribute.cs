using System;

namespace TKW.Framework.Domain.SourceGenerator.Attributes
{
    /// <summary>
    /// 标记需要自动生成代码的类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class GenerateCodeAttribute : Attribute
    {
        public static string FullName => typeof(GenerateCodeAttribute).FullName;

        /// <summary>
        /// 生成类型（如 "Dto"、"Model"、"Service" 等）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 生成类的名称（不指定则自动生成）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 生成类的命名空间（不指定则使用默认规则）
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// 生成类的后缀（当 Name 未指定时生效）
        /// </summary>
        public string Suffix { get; set; }

        /// <summary>
        /// 生成类的版本号
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// 获取生成类的完整名称
        /// </summary>
        public string GetFullName()
        {
            return $"{Namespace}.{Name}{Suffix}{Version}";
        }

        /// <summary>
        /// 获取规范化的生成类型名称（小写）
        /// </summary>
        public string GetNormalizedType()
        {
            return Type.ToLowerInvariant();
        }
    }
}