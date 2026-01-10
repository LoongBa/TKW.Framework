namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 数据转换回调（通知型）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <param name="rowIndex">行索引</param>
/// <param name="entity">当前实体</param>
/// <param name="rawData">原始行数据</param>
public delegate void RecordConvertingCallback<in T>(int rowIndex, T entity, Dictionary<string, object?> rawData);