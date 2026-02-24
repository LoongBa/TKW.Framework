using System.Collections.Generic;
using System.Text.Json;

namespace xCodeGen.Core.Configuration
{
    public class CodeGenConfig
    {
        // 核心路径配置
        public string TargetProject { get; set; } = string.Empty; // 目标项目路径
        public string OutputRoot { get; set; } = "Generated";    // 输出根目录
        public string TemplatesPath { get; set; } = "Templates"; // 模板所在目录
        // 增量生成/防抖动开关
        public bool EnableSkipUnchanged { get; set; } = true;
        // 调试配置
        public DebugConfig Debug { get; set; } = new();

        // 命名与映射规则
        public List<NamingRule> NamingRules { get; set; } = [];
        // 文件名生成模式 (e.g. "{ClassName}.Dto.generated.cs")
        public Dictionary<string, string> FileNamePatterns { get; set; } = new();
        public Dictionary<string, string> TemplateMappings { get; set; } = new(); // ArtifactType -> TemplatePath
        public Dictionary<string, string> OutputDirectories { get; set; } = new(); // ArtifactType -> SubDir
        public Dictionary<string, string> SkeletonMappings { get; set; } = new(); // ArtifactType -> SkeletonTemplatePath
        public Dictionary<string, string> CustomSettings { get; set; } = new();

        public static CodeGenConfig FromJson(string json) =>
            JsonSerializer.Deserialize<CodeGenConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public class DebugConfig
    {
        public bool Enabled { get; set; } = true;
        public string Directory { get; set; } = "_Debug";
    }
}