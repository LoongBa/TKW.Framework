namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 验证结果枚举
/// </summary>
public enum RecordValidateResultEnum
{
    /// <summary>保留当前记录</summary>
    Keep = 0,
    /// <summary>跳过当前记录</summary>
    Skip = 1,
    /// <summary>终止整个导入流程</summary>
    Terminate = 2
}