namespace TKWF.DMPCore.Models;
/// <summary>
/// 只读统计结果
/// </summary>
public record FrozenMetricResult(
    string MetricName,
    Guid MetricDefinitionId,
    object Value,
    string Unit,
    TimeRange TimeRange,
    string Frequency,
    string[] DimensionNames,
    string[] DimensionValues,
    Guid OwnerId,
    string OrgCode,
    string BusinessCategory,
    int TotalRecords,
    DateTime CalculationTime,
    IReadOnlyDictionary<string, object> Metadata
);