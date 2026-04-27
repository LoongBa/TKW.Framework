// IProjectMetaContext.cs - 兼容 C# 7.3
using System.Collections.Generic;

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
        IEnumerable<DomainServiceRegistration> GetServiceRegistrations();
        MethodMetadata GetMethodMeta(string classFullName, string methodName);

        /// <summary>
        /// 高性能获取属性字典的方法，供 ValidationCache 调用
        /// </summary>
        IReadOnlyDictionary<string, PropertyMetadata> GetPropertyMap(string className);

        // 新增：按角色区分的集合，便于运行时/CLI 筛选
        IReadOnlyList<ClassMetadata> Entities { get; }
        IReadOnlyList<ClassMetadata> Services { get; }
        IReadOnlyList<ClassMetadata> Controllers { get; }
        IReadOnlyList<ClassMetadata> Decorators { get; }
    }
}