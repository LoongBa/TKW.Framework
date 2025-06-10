using System.Reflection;
using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core;
/// <summary>
/// 插件加载器实现
/// </summary>
/// <typeparam name="T"></typeparam>
public class DefaultPluginLoader<T> : IPluginLoader<T> where T : class
{
    public IEnumerable<T> LoadPlugins(PluginLoadOptions options)
    {
        return options.LoadMode switch
        {
            "dynamic" => LoadFromDll(options.DllPaths),
            "direct" => LoadFromDebugAssemblies(options.DebugAssemblies),
            _ => throw new NotSupportedException($"不支持的加载模式: {options.LoadMode}")
        };
    }

    private static IEnumerable<T> LoadFromDll(string[]? dllPaths)
    {
        if (dllPaths == null || dllPaths.Length == 0)
            yield break;

        foreach (var dllPath in dllPaths)
        {
            if (!File.Exists(dllPath))
                continue;

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载插件 {dllPath} 失败: {ex.Message}");
                continue; // Skip this DLL and proceed to the next one
            }

            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(T).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is T instance) // Ensure null safety
                    {
                        yield return instance;
                    }
                }
            }
        }
    }

    private static IEnumerable<T> LoadFromDebugAssemblies(string[] assemblyNames)
    {
        if (assemblyNames == null || assemblyNames.Length == 0)
            yield break;

        foreach (var assemblyName in assemblyNames)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
            {
                Console.WriteLine($"调试程序集未找到: {assemblyName}");
                continue;
            }

            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(T).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is T instance) // Ensure null safety
                    {
                        yield return instance;
                    }
                }
            }
        }
    }
}