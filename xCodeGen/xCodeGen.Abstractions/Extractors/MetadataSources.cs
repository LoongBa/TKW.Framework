namespace xCodeGen.Abstractions.Extractors
{
    /// <summary>
    /// 元数据来源类型
    /// </summary>
    public enum MetadataSource
    {
        /// <summary>
        /// 代码文件（C#类/接口）
        /// </summary>
        Code,
    
        /// <summary>
        /// 数据库表结构
        /// </summary>
        Database,
    
        /// <summary>
        /// JSON/XML Schema
        /// </summary>
        Schema
    }
}
