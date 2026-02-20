using System.Collections.Generic;
using xCodeGen.Abstractions.Extractors;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 原始元数据（提取器输出的未加工数据）
    /// </summary>
    public class RawMetadata
    {
        /// <summary>
        /// 来源唯一标识（类名/表名）
        /// </summary>
        public string SourceId { get; set; } = string.Empty;
    
        /// <summary>
        /// 来源类型（Class/Table等）
        /// </summary>
        public MetadataSource SourceType { get; set; } = MetadataSource.Code;
    
        /// <summary>
        /// 结构化元数据内容
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    
        /// <summary>
        /// 提取过程日志
        /// </summary>
        public List<string> ExtractionLogs { get; set; } = new List<string>();
    }
}
