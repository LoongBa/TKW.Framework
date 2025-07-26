using System.Collections.Generic;
using System.Linq;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 类的元数据
    /// </summary>
    public class ClassMetadata
    {
        /// <summary>
        /// 命名空间
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// 类名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 类的全限定名
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// 类中包含的方法元数据
        /// </summary>
        public List<MethodMetadata> Methods { get; set; } = new List<MethodMetadata>();
    }
    /// <summary>
    /// 提取的元数据（类集合）
    /// </summary>
    public class ExtractedMetadata
    {
        /// <summary>
        /// 提取到的类元数据集合
        /// </summary>
        public List<ClassMetadata> Classes { get; set; } = new List<ClassMetadata>();
    }
    /// <summary>
    /// 方法的元数据
    /// </summary>
    public class MethodMetadata
    {
        /// <summary>
        /// 方法名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 返回类型
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// 是否为异步方法
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// 方法参数元数据
        /// </summary>
        public List<ParameterMetadata> Parameters { get; set; } = new List<ParameterMetadata>();

        /// <summary>
        /// 方法的唯一签名（用于处理重载）
        /// </summary>
        public string UniqueSignature
        {
            get
            {
                string paramTypes = string.Join("_", Parameters.Select(p => p.TypeName.Replace("?", "Nullable").Replace("<", "_").Replace(">", "_")));
                return $"{Name}_{paramTypes}";
            }
        }
    }
    /// <summary>
    /// 参数的元数据
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数类型名称（短名称）
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 参数类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 是否为可空类型
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// 是否为集合类型
        /// </summary>
        public bool IsCollection { get; set; }

        /// <summary>
        /// 集合元素类型
        /// </summary>
        public string CollectionItemType { get; set; }

        /// <summary>
        /// 参数上的特性
        /// </summary>
        public List<AttributeMetadata> Attributes { get; set; } = new List<AttributeMetadata>();
    }

    /// <summary>
    /// 特性的元数据
    /// </summary>
    public class AttributeMetadata
    {
        /// <summary>
        /// 特性类型全限定名
        /// </summary>
        public string TypeFullName { get; set; }

        /// <summary>
        /// 特性参数
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
    