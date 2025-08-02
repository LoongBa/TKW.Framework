using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 特性的元数据
    /// </summary>
    public class AttributeMetadata
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 特性类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 特性参数
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public ICollection<object> ConstructorArguments { get; set; } = new Collection<object>();
        public Dictionary<string, object> NamedArguments { get; set; } = new Dictionary<string, object>();

    }
}