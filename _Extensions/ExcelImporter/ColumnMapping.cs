using System.Collections.Specialized;

namespace TKWF.ExcelImporter;

/// <summary>
/// 列映射配置
/// </summary>
public class ColumnMapping
{
    public string ExcelColumnName { get; set; } = string.Empty;
    public string TargetFieldName { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; } = false;
    public string FormatPattern { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;

    public static List<ColumnMapping> CreateFrom(Dictionary<string, string> columns)
    {
        List<ColumnMapping> columnMappings = new();
        foreach (var column in columns.Keys)
        {
            var columnName = column;
            columnMappings.Add(new ColumnMapping()
            {
                ExcelColumnName = columnName,
                TargetFieldName = columns[columnName!],
                DefaultValue = string.Empty,
                DataType = "string",
                FormatPattern = string.Empty,
                IsRequired = false,
            });
        }
        return columnMappings;
    }

}