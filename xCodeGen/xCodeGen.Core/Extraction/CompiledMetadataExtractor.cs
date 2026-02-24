using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction;

/// <summary>
/// 提取已编译好的元数据对象并包装
/// </summary>
public class CompiledMetadataExtractor(IProjectMetaContext context) : IMetaDataExtractor
{
    public MetadataSource SourceType => MetadataSource.Code;

    public Task<IEnumerable<RawMetadata>> ExtractAsync(
        ExtractorOptions options,
        CancellationToken cancellationToken = default)
    {
        if (context?.AllMetadatas == null)
            return Task.FromResult(Enumerable.Empty<RawMetadata>());

        var result = context.AllMetadatas.Select(meta => new RawMetadata
        {
            SourceType = SourceType,
            SourceId = meta.FullName,
            Data = new Dictionary<string, object>
            {
                { "Object", meta } // MetadataConverter 将从此解析出 ClassMetadata
            }
        });

        return Task.FromResult(result);
    }
}