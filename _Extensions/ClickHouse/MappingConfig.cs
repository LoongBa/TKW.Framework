namespace TKWF.Extensions.ClickHouse;

public class MappingConfig
{
    public List<PropertyMapping> PropertyMappings { get; set; } = new();
    public List<FieldConverter> FieldConverters { get; set; } = new();
}