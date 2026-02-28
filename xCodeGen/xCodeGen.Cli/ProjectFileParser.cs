using System.Xml.Linq;

namespace xCodeGen.Cli;

public static class ProjectFileParser
{
    public static string? GetAssemblyPath(string projectPath)
    {
        var doc = XDocument.Load(projectPath);
        var rootDir = Path.GetDirectoryName(projectPath);

        // 获取程序集名称，若无则默认为文件名
        var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value
                           ?? Path.GetFileNameWithoutExtension(projectPath);

        // 简单查找 bin 目录下最新的该程序集
        var binPath = Path.Combine(rootDir, "bin");
        if (!Directory.Exists(binPath)) return null;

        return Directory.GetFiles(binPath, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }
}