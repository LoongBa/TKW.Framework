namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 原始元数据转换器接口
    /// </summary>
    public interface IMetadataConverter
    {
        /// <summary>
        /// 将原始元数据转换为抽象元数据接口
        /// </summary>
        /// <param name="rawMetadata">原始元数据</param>
        /// <returns>抽象元数据接口（转换失败返回null）</returns>
        ClassMetadata Convert(RawMetadata rawMetadata);
    }
}
