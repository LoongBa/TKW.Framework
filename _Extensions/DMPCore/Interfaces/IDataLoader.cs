using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Interfaces;

// 数据加载插件接口
public interface IDataLoader
{
    string SourceType { get; }
    IEnumerable<Dictionary<string, object>> Load(DataLoadOptions options);
}