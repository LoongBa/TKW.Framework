namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 泛型Excel记录验证回调（处理前/处理中，用于业务校验并控制流程）
/// </summary>
/// <typeparam name="T">目标类型</typeparam>
/// <param name="rowIndex">当前行索引（从0开始）</param>
/// <param name="record">当前创建的对象实例（已完成属性赋值）</param>
/// <param name="rowValues">当前行原始值字典（Excel列名->单元格值）</param>
/// <param name="otherColumnValues">未映射列的原始值字典</param>
/// <param name="failure">失败信息对象（用户可自定义赋值错误信息/异常）</param>
/// <returns>处理状态指令</returns>
public delegate ExcelRecordProcessStatus ExcelRecordValidatingCallback<T>(
    int rowIndex,
    T record,
    Dictionary<string, object?> rowValues,
    Dictionary<string, object?> otherColumnValues,
    ref ExcelImportFailure failure);