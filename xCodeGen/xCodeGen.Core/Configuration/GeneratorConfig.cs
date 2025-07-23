using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace xCodeGen.Core.Configuration
{
    /// <summary>
    /// 生成器配置
    /// </summary>
    public class GeneratorConfig
    {
        /// <summary>
        /// 目标项目路径
        /// </summary>
        public string TargetProject { get; set; }

        /// <summary>
        /// 输出根目录
        /// </summary>
        public string OutputRoot { get; set; }

        /// <summary>
        /// 调试配置
        /// </summary>
        public DebugConfig Debug { get; set; } = new DebugConfig();

        /// <summary>
        /// 模板映射配置
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> TemplateMappings { get; set; } = 
            new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// 输出目录配置
        /// </summary>
        public Dictionary<string, string> OutputDirectories { get; set; } = 
            new Dictionary<string, string>();

        /// <summary>
        /// 启用的工具类
        /// </summary>
        public List<string> EnabledUtilities { get; set; } = new List<string>();

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static GeneratorConfig LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("配置文件不存在", path);
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<GeneratorConfig>(json) 
                ?? throw new InvalidOperationException("无法解析配置文件");
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void SaveToFile(string path)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    /// <summary>
    /// 调试配置
    /// </summary>
    public class DebugConfig
    {
        /// <summary>
        /// 是否启用调试
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 调试目录
        /// </summary>
        public string Directory { get; set; } = "_Debug";
    }
}
    