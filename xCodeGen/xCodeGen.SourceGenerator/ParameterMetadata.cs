using System.Collections.Generic;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 参数的元数据
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数类型名称（短名称）
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 参数类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 是否为可空类型
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// 是否为集合类型
        /// </summary>
        public bool IsCollection { get; set; }

        /// <summary>
        /// 集合元素类型
        /// </summary>
        public string CollectionItemType { get; set; }

        /// <summary>
        /// 参数上的特性
        /// </summary>
        public List<AttributeMetadata> Attributes { get; set; } = new List<AttributeMetadata>();
    }
}