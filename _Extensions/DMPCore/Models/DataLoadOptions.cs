namespace TKWF.DMP.Core.Models;
/// <summary>
/// 数据加载选项
/// </summary>
public record DataLoadOptions(
    string ConnectionString,
    string Query,
    DateTime StartTime,
    DateTime EndTime
);