namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 行级转换结果（内部核心结构，流式返回每行结果）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class EntityConvertResult<T>
{
    /// <summary>是否处理成功</summary>
    public bool Success { get; set; }

    /// <summary>处理成功的实体（失败时为null）</summary>
    public T? Entity { get; set; }

    /// <summary>失败信息（成功时为null）</summary>
    public ImportFailure? Failure { get; set; }

    /// <summary>行索引</summary>
    public int RowIndex { get; set; }
}