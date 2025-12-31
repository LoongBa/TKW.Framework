namespace TKWF.Tools.ExcelTools;

/// <summary>
/// Excel记录验证/处理状态（回调返回值，用于控制流程走向）
/// </summary>
public enum ExcelRecordProcessStatus
{
    /// <summary>
    /// 继续处理（本条正常校验，符合条件则加入成功列表）
    /// </summary>
    Continue = 0,

    /// <summary>
    /// 忽略本条（不加入成功列表，也不记入失败明细，直接跳过）
    /// </summary>
    Skip = 1,

    /// <summary>
    /// 本条失败（记入失败明细，继续处理后续行）
    /// </summary>
    Fail = 2,

    /// <summary>
    /// 终止所有处理（本条记入失败明细，立即停止后续所有行的处理）
    /// </summary>
    Abort = 3
}