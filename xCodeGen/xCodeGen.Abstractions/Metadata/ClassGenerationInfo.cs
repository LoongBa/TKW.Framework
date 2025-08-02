using Microsoft.CodeAnalysis;

namespace xCodeGen.Abstractions.Metadata
{
    public class ClassGenerationInfo
    {
        public ClassMetadata Metadata { get; set; }
        public string Type { get; set; }
        public string GenerateMode { get; set; }
        public string TemplateName { get; set; }
        public bool Overwrite { get; set; }
        public SemanticModel SemanticModel { get; set; }
    }
}