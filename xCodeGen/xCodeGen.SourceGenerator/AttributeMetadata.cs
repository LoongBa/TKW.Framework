using System.Collections.Generic;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 特性的元数据
    /// </summary>
    public class AttributeMetadata
    {
        /// <summary>
        /// 特性类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 特性参数
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}