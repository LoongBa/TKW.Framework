using System.Collections.Generic;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Abstractions.Metadata
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
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// 类中包含的方法元数据
        /// </summary>
        public List<MethodMetadata> Methods { get; set; } = new List<MethodMetadata>();

        /// <summary>
        /// 类上的生成特性
        /// </summary>
        public GenerateArtifactAttribute GenerateArtifactAttribute { get; set; }
    }
}
    