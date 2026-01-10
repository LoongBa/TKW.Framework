namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 数据验证回调（可控流程）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <param name="rowIndex">行索引</param>
/// <param name="entity">当前实体</param>
/// <param name="rawData">原始行数据</param>
/// <param name="failure">失败信息</param>
/// <returns>验证结果（决定是否保留/跳过/终止）</returns>
public delegate RecordValidateResultEnum RecordValidatingCallback<in T>(
    int rowIndex, T entity, Dictionary<string, object?> rawData, out ImportFailure? failure);