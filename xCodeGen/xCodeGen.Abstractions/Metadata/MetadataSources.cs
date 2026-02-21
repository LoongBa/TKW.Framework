namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 元数据来源类型
    /// </summary>
    public enum MetadataSource
    {
        Error = -1,
        /// <summary>
        /// 代码文件（C#类/接口）
        /// </summary>
        Code = 0,
    
        /// <summary>
        /// 数据库表结构
        /// </summary>
        Database = 1,
    
        /// <summary>
        /// JSON/XML Schema
        /// </summary>
        Schema = 2,
    }
}
