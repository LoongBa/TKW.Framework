using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 数据导出器接口
/// </summary>
public interface IDataExporter<in T>
    where T : class, new()
{
    void Export(T result, DataExportConfig config);
}