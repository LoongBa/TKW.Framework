using System;
using System.IO;

namespace xCodeGen.Core.IO;

/// <summary>
/// 基于本地文件系统的文件写入实现
/// </summary>
public class FileSystemWriter : IFileWriter
{
    /// <summary>
    /// 解析基础路径与文件名的组合路径
    /// </summary>
    /// <param name="basePath">基础路径</param>
    /// <param name="fileName">文件名</param>
    /// <returns>组合后的完整路径</returns>
    public string ResolvePath(string basePath, string fileName)
    {
        if (string.IsNullOrEmpty(basePath)) return fileName;
        if (string.IsNullOrEmpty(fileName)) return basePath;

        // 处理跨平台路径分隔符
        return Path.Combine(basePath, fileName);
    }

    /// <summary>
    /// 解析输出文件路径
    /// </summary>
    /// <param name="basePath">基础路径</param>
    /// <param name="className">类名</param>
    /// <param name="fileNameFormat">文件名格式（含占位符）</param>
    public string ResolveOutputPath(string basePath, string className, string fileNameFormat)
    {
        // 1. 应用文件名格式（业务逻辑）
        var fileName = fileNameFormat.Replace("{ClassName}", className);
        // 2. 复用基础路径拼接能力（去重）
        return ResolvePath(basePath, fileName);
    }

    public void Write(string content, string filePath, bool overwrite)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        // 不覆盖且文件已存在时直接返回
        if (!overwrite && Exists(filePath))
            return;

        // 写入文件内容
        File.WriteAllText(filePath, content);
    }

    public bool Exists(string filePath)
    {
        return File.Exists(filePath);
    }
}