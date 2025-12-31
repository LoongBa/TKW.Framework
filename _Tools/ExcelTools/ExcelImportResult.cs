namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 导入结果（包含成功项与失败明细）
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ExcelImportResult<T>
{
    /// <summary>
    /// 成功导入的数据列表
    /// </summary>
    public List<T> Items { get; } = [];

    /// <summary>
    /// 导入失败的明细列表
    /// </summary>
    public List<ExcelImportFailure> Failures { get; } = [];

    /// <summary>
    /// 成功导入数量
    /// </summary>
    public int SuccessCount => Items.Count;

    /// <summary>
    /// 导入失败数量
    /// </summary>
    public int FailedCount => Failures.Count;
}