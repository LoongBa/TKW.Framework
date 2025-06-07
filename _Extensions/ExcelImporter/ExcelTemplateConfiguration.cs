namespace TKWF.ExcelImporter;

/// <summary>
/// Excel导入模板配置
/// </summary>
public class ExcelTemplateConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime Version { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
    public string DataCategory { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public bool HasHeader { get; set; } = true;
    public int StartRowIndex { get; set; } = 1;
    public string TargetTypeName { get; set; } = string.Empty;
    public List<ColumnMapping> ColumnMappings { get; set; } = [];
    public Dictionary<string, string> Extensions { get; set; } = [];
    public List<string> RowValidations { get; set; } = [];
}