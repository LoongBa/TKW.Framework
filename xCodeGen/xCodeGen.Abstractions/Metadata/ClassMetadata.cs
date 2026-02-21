using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using xCodeGen.Abstractions.Extractors;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 类的元数据
    /// </summary>
    public class ClassMetadata
    {
        // 基础标识信息
        public string Namespace { get; set; }
        public string ClassName { get; set; }
        public string FullName { get; set; }
        public string Summary { get; set; } = string.Empty;

        // 结构信息
        public ICollection<MethodMetadata> Methods { get; set; }
        public ICollection<PropertyMetadata> Properties { get; set; }
        public ICollection<string> ImplementedInterfaces { get; set; }
        public ICollection<string> DependentTypes { get; set; }
        public string BaseType { get; set; }

        // 生成配置信息
        public string Mode { get; set; }
        public MetadataSource SourceType { get; set; }
        public string TemplateName { get; set; }
        public Dictionary<string, object> GenerateCodeSettings { get; set; }
        public ICollection<AttributeMetadata> Attributes { get; set; }

        // 版本与变更跟踪
        public string SourceHash { get; set; }
        public string PreviousHash { get; set; }
        public string Version { get; set; }
        public string GeneratorVersion { get; set; }
        public bool IsIncrementalUpdate { get; set; }

        // 源文件信息
        public string SourceFilePath { get; set; }
        public int SourceLineNumber { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }

        // 生成器标识
        public string GeneratorId { get; set; }
        public string GeneratorDescription { get; set; }
        public bool IsRecord { get; set; }
        public string TypeKind { get; set; }

        /// <summary>
        /// 初始化 ClassMetadata 实例
        /// </summary>
        public ClassMetadata()
        {
            // 初始化字符串属性为空字符串（避免 null）
            Namespace = string.Empty;
            ClassName = string.Empty;
            FullName = string.Empty;
            BaseType = string.Empty;
            Mode = string.Empty;
            SourceType = MetadataSource.Code;
            TemplateName = string.Empty;
            SourceHash = string.Empty;
            PreviousHash = string.Empty;
            Version = "1.0.0"; // 默认版本
            GeneratorVersion = string.Empty;
            SourceFilePath = string.Empty;
            GeneratorId = string.Empty;
            GeneratorDescription = string.Empty;

            // 初始化集合属性（避免 null 引用）
            Methods = new Collection<MethodMetadata>();
            Properties = new Collection<PropertyMetadata>();
            ImplementedInterfaces = new Collection<string>();
            DependentTypes = new Collection<string>();
            GenerateCodeSettings = new Dictionary<string, object>();
            Attributes = new Collection<AttributeMetadata>();

            // 初始化时间（默认为 UTC 时间）
            CreatedTime = DateTime.UtcNow;
            UpdatedTime = DateTime.UtcNow;
        }
    }
}