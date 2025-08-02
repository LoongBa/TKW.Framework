namespace xCodeGen.Abstractions.Metadata
{
    public class PropertyMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string AccessModifier { get; set; } = "public";
        public bool IsReadOnly { get; set; }
        public bool IsStatic { get; set; }
    }
}