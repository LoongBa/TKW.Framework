using System.Collections.Generic;

namespace xCodeGen.Abstractions.Extractors
{
    /// <summary>
    /// 提取器的输入参数选项
    /// </summary>
    public class ExtractorOptions
    {
        /// <summary>
        /// 项目路径
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>
        /// 元数据输出路径
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// 提取器专用配置（键值对形式）
        /// </summary>
        public Dictionary<string, object> ExtractorConfig { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableDetailedLogging { get; set; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public int TimeoutMs { get; set; } = 30000; // 默认30秒
    }
}
