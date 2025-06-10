using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

// 数据加载插件接口
public interface IDataLoader<out T>
where T : class, new()
{
    string SourceType { get; }
    IEnumerable<T> Load(DataLoadOptions options);
}