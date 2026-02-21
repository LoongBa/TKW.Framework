using System;
using System.Collections.Generic;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 元数据变更日志
    /// </summary>
    public class MetadataChangeLog
    {
        public List<ClassMetadata> Added { get; } = [];
        public List<ClassMetadata> Modified { get; } = [];
        public List<string> RemovedClassNames { get; } = [];
        public DateTime GenerationTimestamp { get; set; } = DateTime.UtcNow;
    }
}