namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 转换结果封装（包含是否成功+值+错误信息）
/// </summary>
public class ConvertResult
{
    /// <summary>是否转换成功</summary>
    public bool Success { get; set; }

    /// <summary>转换后的值（失败则为默认值）</summary>
    public object? Value { get; set; }

    /// <summary>失败原因（便于调用者收集）</summary>
    public string? ErrorMessage { get; set; }
}