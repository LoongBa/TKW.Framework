namespace xCodeGen.Core.IO;

/// <summary>
/// 文件写入接口，封装文件创建、内容写入等操作
/// </summary>
public interface IFileWriter
{
    /// <summary>
    /// 解析基础路径与文件名的组合路径
    /// </summary>
    /// <param name="basePath">基础路径</param>
    /// <param name="fileName">文件名</param>
    /// <returns>组合后的完整路径</returns>
    string ResolvePath(string basePath, string fileName);  // 新增：解决路径组合问题

    /// <summary>
    /// 解析输出文件路径
    /// </summary>
    /// <param name="basePath">基础路径</param>
    /// <param name="className">类名</param>
    /// <param name="fileNameFormat">文件名格式（含占位符）</param>
    string ResolveOutputPath(string basePath, string className, string fileNameFormat);

    /// <summary>
    /// 写入文件内容
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="overwrite">是否覆盖已有文件</param>
    void Write(string content, string filePath, bool overwrite);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    bool Exists(string filePath);
}