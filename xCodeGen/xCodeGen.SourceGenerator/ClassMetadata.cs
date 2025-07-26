using System.Collections.Generic;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 类的元数据
    /// </summary>
    public class ClassMetadata
    {
        /// <summary>
        /// 命名空间
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// 类名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 类的全限定名
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// 类中包含的方法元数据
        /// </summary>
        public List<MethodMetadata> Methods { get; set; } = new List<MethodMetadata>();
    }
}
    