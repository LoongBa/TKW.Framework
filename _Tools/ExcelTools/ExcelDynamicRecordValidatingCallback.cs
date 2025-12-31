namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 动态Excel记录验证回调（处理前/处理中，用于业务校验并控制流程）
/// </summary>
/// <param name="rowIndex">当前行索引（从0开始）</param>
/// <param name="dynamicRecord">当前创建的动态对象实例（已完成属性赋值）</param>
/// <param name="rowValues">当前行原始值字典（Excel列名->单元格值）</param>
/// <param name="failure">失败信息对象（用户可自定义赋值错误信息/异常）</param>
/// <returns>处理状态指令</returns>
public delegate ExcelRecordProcessStatus ExcelDynamicRecordValidatingCallback(
    int rowIndex,
    dynamic dynamicRecord,
    Dictionary<string, object?> rowValues,
    ref ExcelImportFailure failure);