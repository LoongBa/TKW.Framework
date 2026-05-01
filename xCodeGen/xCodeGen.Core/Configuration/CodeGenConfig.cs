using System.Collections.Generic;
using System.Text.Json;

namespace xCodeGen.Core.Configuration
{
    public class CodeGenConfig
    {
        // === 基础设置 ===
        public string TargetProject { get; set; } = string.Empty;
        public string OutputRoot { get; set; } = "Generated";
        public string TemplatesPath { get; set; } = "Templates";
        public bool EnableSkipUnchanged { get; set; } = true;
        public DebugConfig Debug { get; set; } = new();
        public string InterfaceOutputPath { get; set; } = "Interfaces";
        public string ServiceDirectory { get; set; } = "Services";
        public List<string> AttributeWhitelist { get; set; } = [];
        public List<NamingRule> NamingRules { get; set; } = [];
        // === 产物生成核心配置 ===
        /// <summary>
        /// Key 为产物类型名称，如 "Entity", "Dto", "DomainMap"
        /// </summary>
        public Dictionary<string, ArtifactConfig> Artifacts { get; set; } = new();

        // === 自定义设置 ===
        public Dictionary<string, string> CustomSettings { get; set; } = new();

        public static CodeGenConfig FromJson(string json) =>
            JsonSerializer.Deserialize<CodeGenConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    /// <summary>
    /// 单个生成产物的配置定义
    /// </summary>
    public class ArtifactConfig
    {
        /// <summary> 
        /// 生成范围: "Entity" (遍历实体生成), "Project" (全局只生成一次) 
        /// </summary>
        public string Scope { get; set; } = "Entity";

        // ---- 可覆盖生成的模板配置 (受 EnableSkipUnchanged 控制) ----
        public string? Template { get; set; }
        public string OutputPattern { get; set; } = "{ClassName}.g.cs";
        public string OutputDir { get; set; } = "";

        // ---- 一次性骨架模板配置 (文件存在则跳过) ----
        public string? SkeletonTemplate { get; set; }
        public string? SkeletonPattern { get; set; }
        public string? SkeletonDir { get; set; }
    }

    public class DebugConfig
    {
        public bool Enabled { get; set; } = true;
        public string Directory { get; set; } = "_Debug";
    }
}