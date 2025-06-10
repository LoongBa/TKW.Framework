using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 数据加载插件接口
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IDataLoader<out T>
where T : class, new()
{
    string SourceType { get; }
    IEnumerable<T> Load(DataLoadingConfig config);
}