using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 项目元数据上下文基类：提供单例挂载点与高性能属性字典缓存
    /// </summary>
    public abstract class ProjectMetaContextBase : IProjectMetaContext
    {
        // 全局单例引用，由生成的代码子类在初始化时赋值
        public static IProjectMetaContext Instance { get; protected set; }

        // 缓存：类名 -> (属性名 -> 属性元数据)，确保运行期查找为 O(1)
        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, PropertyMetadata>> _propCache =
            new ConcurrentDictionary<string, IReadOnlyDictionary<string, PropertyMetadata>>();

        protected readonly List<ClassMetadata> _allMetadatas = new List<ClassMetadata>();

        public abstract ProjectConfiguration Configuration { get; }

        public abstract MetadataChangeLog ChangeLog { get; }

        public abstract string MetadataSchemaVersion { get; }

        public IReadOnlyList<ClassMetadata> AllMetadatas => _allMetadatas.AsReadOnly();
        /// <summary>
        /// 新增抽象属性：按角色筛选的集合（具体实现由生成的 ProjectMetaContext 提供）
        /// </summary>
        public IReadOnlyList<ClassMetadata> Entities => _allMetadatas.Where(m => m.GenerateCodeSettings.ContainsKey("IsEntity") && (bool)m.GenerateCodeSettings["IsEntity"]).ToList().AsReadOnly();

        public IReadOnlyList<ClassMetadata> Services => _allMetadatas.Where(m => (m.ImplementedInterfaces != null && m.ImplementedInterfaces.Any(i => !string.IsNullOrEmpty(i) && i.IndexOf("IDomainService", StringComparison.OrdinalIgnoreCase) >= 0)) || (!string.IsNullOrEmpty(m.ClassName) && m.ClassName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))).ToList().AsReadOnly();

        public IReadOnlyList<ClassMetadata> Controllers => _allMetadatas.Where(m => m.IsController).ToList().AsReadOnly();

        public IReadOnlyList<ClassMetadata> Decorators => _allMetadatas.Where(m => !string.IsNullOrEmpty(m.DecoratorTypeFullName) || m.HasDecoratorCandidate).ToList().AsReadOnly();


        public virtual ClassMetadata FindByClassName(string className)
            => AllMetadatas.FirstOrDefault(m => m.ClassName == className);

        public virtual IEnumerable<ClassMetadata> FindByNamespace(string @namespace)
            => AllMetadatas.Where(m => m.Namespace == @namespace);

        /// <summary>
        /// 高性能获取属性字典的方法，供 ValidationCache 调用
        /// </summary>
        public IReadOnlyDictionary<string, PropertyMetadata> GetPropertyMap(string className)
        {
            return _propCache.GetOrAdd(className, name =>
            {
                var meta = FindByClassName(name);
                // 将 Collection 转换为 Dictionary 提升查找性能
                return meta?.Properties.ToDictionary(p => p.Name)
                       ?? new Dictionary<string, PropertyMetadata>();
            });
        }
    }
}