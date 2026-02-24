using System.Collections.Generic;

namespace xCodeGen.Abstractions.Metadata
{
    public class PropertyMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string AccessModifier { get; set; } = "public";
        public bool IsReadOnly { get; set; }
        public bool IsStatic { get; set; }
        public string TypeFullName { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string DisplayName { get; set; } // 来自 DtoFieldAttribute
        public bool IsSearchable { get; set; }  // 来自 DtoFieldAttribute
        public bool IsUnique { get; set; }  // 来自 DtoFieldAttribute
        public List<AttributeMetadata> Attributes { get; set; } = new List<AttributeMetadata>();
    }
}