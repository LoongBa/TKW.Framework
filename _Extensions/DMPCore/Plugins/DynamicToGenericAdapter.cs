using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins;
/// <summary>
/// 字典到对象的转换器
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="dynamicLoader"></param>
/// <param name="converter"></param>
/// <param name="sourceType"></param>
public class DynamicToGenericAdapter<T>(
    IDynamicDataLoader dynamicLoader,
    IDataConverter<T> converter,
    string sourceType)
    : IDataLoader<T>
    where T : class, new()
{
    public string SourceType { get; } = sourceType;

    public IEnumerable<T> Load(DataLoadOptions options)
    {
        return dynamicLoader.Load(options)
            .Select(converter.Convert);
    }
}