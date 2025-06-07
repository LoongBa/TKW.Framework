namespace TKWF.ExcelImporter;

/// <summary>
/// 导入结果
/// </summary>
public class ImportResult<T>
{
    public List<T> Data { get; set; } = [];
    public List<ImportError> Errors { get; set; } = [];
    public int TotalRows { get; set; }
    public int SuccessRows => TotalRows - Errors.Count;
}