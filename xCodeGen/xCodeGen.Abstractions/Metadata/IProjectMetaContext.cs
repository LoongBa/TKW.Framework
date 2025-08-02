// IProjectMetaContext.cs - 兼容 C# 7.3

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 项目元数据上下文的接口约定
    /// </summary>
    public interface IProjectMetaContext
    {
        IReadOnlyList<ClassMetadata> AllMetadatas { get; }
        ProjectConfiguration Configuration { get; }
        MetadataChangeLog ChangeLog { get; }
        string MetadataSchemaVersion { get; }

        ClassMetadata FindByClassName(string className);
        IEnumerable<ClassMetadata> FindByNamespace(string @namespace);
    }
}