using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 特性的元数据
    /// </summary>
    public class AttributeMetadata
    {
        /// <summary> 特性短名称 </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 特性类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 特性命名参数（主要存储通过 Property=Value 形式定义的参数）
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 构造函数参数集合（按顺序存储）
        /// </summary>
        public ICollection<object> ConstructorArguments { get; set; } = new Collection<object>();

        /// <summary>
        /// 命名参数字典（为了兼容性保留，通常与 Properties 指向同一内容）
        /// </summary>
        public Dictionary<string, object> NamedArguments { get; set; } = new Dictionary<string, object>();
    }
}