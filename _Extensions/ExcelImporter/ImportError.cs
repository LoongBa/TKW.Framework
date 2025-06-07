using Microsoft.Extensions.Localization;

namespace TKWF.ExcelImporter;

/// <summary>
/// 导入错误
/// </summary>
public class ImportError
{
    /// <summary>
    /// 导入错误
    /// </summary>
    public ImportError(string errorMessage, string fieldName = "", int rowNumber = 0)
    {
        ErrorMessage = errorMessage;
        RowNumber = rowNumber;
        FieldName = fieldName;
    }

    public ImportError(string fieldName, int rowNumber)
    {
        RowNumber = rowNumber;
        FieldName = fieldName;
    }

    public int RowNumber { get; set; }
    public string FieldName { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}