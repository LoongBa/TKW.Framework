using System.Collections.Generic;

namespace xCodeGen.SourceGenerator
{
    /// <summary>
    /// 提取的元数据（类集合）
    /// </summary>
    public class ExtractedMetadata
    {
        /// <summary>
        /// 提取到的类元数据集合
        /// </summary>
        public List<ClassMetadata> Classes { get; set; } = new List<ClassMetadata>();
    }
}