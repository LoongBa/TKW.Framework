namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 导入失败明细
/// </summary>
public class ExcelImportFailure
{
    /// <summary>
    /// 失败行索引（从0开始）
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// 错误描述信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 异常对象（可选）
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 失败行的原始值字典
    /// </summary>
    public Dictionary<string, object?> RowValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}