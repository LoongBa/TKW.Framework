using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace xCodeGen.Abstractions.Metadata
{
    public class ClassMetadata
    {
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public bool IsView { get; set; } = false;

        public ICollection<MethodMetadata> Methods { get; set; } = new Collection<MethodMetadata>();
        public ICollection<PropertyMetadata> Properties { get; set; } = new Collection<PropertyMetadata>();
        public ICollection<string> ImplementedInterfaces { get; set; } = new Collection<string>();
        public ICollection<string> DependentTypes { get; set; } = new Collection<string>();
        public string BaseType { get; set; } = string.Empty;

        public string Mode { get; set; } = string.Empty;
        public MetadataSource SourceType { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public Dictionary<string, object> GenerateCodeSettings { get; set; } = new Dictionary<string, object>();
        public ICollection<AttributeMetadata> Attributes { get; set; } = new Collection<AttributeMetadata>();

        public string SourceHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string GeneratorVersion { get; set; } = string.Empty;
        public bool IsIncrementalUpdate { get; set; }

        public string SourceFilePath { get; set; } = string.Empty;
        public int SourceLineNumber { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedTime { get; set; } = DateTime.UtcNow;

        public string GeneratorId { get; set; } = string.Empty;
        public string GeneratorDescription { get; set; } = string.Empty;
        public bool IsRecord { get; set; }
        public string TypeKind { get; set; } = "class";

        public ClassMetadata() { }
    }
}