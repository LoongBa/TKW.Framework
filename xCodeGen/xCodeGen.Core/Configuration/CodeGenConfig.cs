using System.Collections.Generic;
using System.Text.Json;

namespace xCodeGen.Core.Configuration
{
    /// <summary>
    /// 代码生成器配置
    /// </summary>
    public class CodeGenConfig
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 输出基础路径
        /// </summary>
        public string OutputBasePath { get; set; } = "Generated";
        
        /// <summary>
        /// 模板路径
        /// </summary>
        public string TemplatesPath { get; set; } = "Templates";
        
        /// <summary>
        /// 命名规则集合
        /// </summary>
        public List<NamingRule> NamingRules { get; set; } = [];
        
        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool? DebugMode { get; set; } = true;
        
        /// <summary>
        /// 从JSON字符串创建配置实例
        /// </summary>
        public static CodeGenConfig FromJson(string json)
        {
            return JsonSerializer.Deserialize<CodeGenConfig>(json, _jsonSerializerOptions) ?? new CodeGenConfig();
        }
        
        /// <summary>
        /// 转换为JSON字符串
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, _jsonSerializerOptions);
        }
    }
    
    /// <summary>
    /// 命名规则
    /// </summary>
    public class NamingRule
    {
        /// <summary>
        /// 产物类型
        /// </summary>
        public string ArtifactType { get; set; }
        
        /// <summary>
        /// 命名模式，可用占位符: {ClassName} - 原始名称, {ArtifactType} - 产物类型
        /// </summary>
        public string Pattern { get; set; } = "{ClassName}{ArtifactType}";
    }
}
