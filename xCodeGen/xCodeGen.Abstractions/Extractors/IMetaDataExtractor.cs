using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Abstractions.Extractors
{
    /// <summary>
    /// 元数据提取器抽象接口
    /// </summary>
    public interface IMetaDataExtractor
    {
        /// <summary>
        /// 提取器对应的元数据来源类型
        /// </summary>
        MetadataSource SourceType { get; }
    
        /// <summary>
        /// 提取元数据
        /// </summary>
        /// <param name="options">提取器选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>原始元数据集合</returns>
        Task<IEnumerable<RawMetadata>> ExtractAsync(
            ExtractorOptions options, 
            CancellationToken cancellationToken = default);
    }
}
