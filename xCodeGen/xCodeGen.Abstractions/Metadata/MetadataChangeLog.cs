using System;
using System.Collections.Generic;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 元数据变更日志
    /// </summary>
    public class MetadataChangeLog
    {
        public List<ClassMetadata> Added { get; } = new List<ClassMetadata>();
        public List<ClassMetadata> Modified { get; } = new List<ClassMetadata>();
        public List<string> RemovedClassNames { get; } = new List<string>();
        public DateTime GenerationTimestamp { get; set; } = DateTime.UtcNow;
    }
}