using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 插件加载器接口
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IPluginLoader<T> where T : class
{
    IEnumerable<T> LoadPlugins(PluginLoadOptions options);
}