using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction;

/// <summary>
/// 固化元数据提取器：直接从加载到内存的 ProjectMetaContext 中提取数据
/// 职责：将编译好的 ClassMetadata 重新包装，以适配 Engine 的流水线逻辑
/// </summary>
public class CompiledMetadataExtractor(IProjectMetaContext context) : IMetaDataExtractor
{
    // 来源标识：Code (表示来源于源代码生成的元数据)
    public MetadataSource SourceType => MetadataSource.Code;

    public Task<IEnumerable<RawMetadata>> ExtractAsync(
        ExtractorOptions options,
        CancellationToken cancellationToken = default)
    {
        if (context?.AllMetadatas == null)
            return Task.FromResult(Enumerable.Empty<RawMetadata>());

        // 将固化好的元数据对象直接透传给 MetadataConverter
        var result = context.AllMetadatas.Select(meta => new RawMetadata
        {
            SourceType = SourceType,
            SourceId = meta.FullName,
            // 关键：将完整的 ClassMetadata 对象放入 Data 字典
            Data = new Dictionary<string, object>
            {
                { "IsCompiled", true },
                { "Object", meta }
            }
        });

        return Task.FromResult(result);
    }
}