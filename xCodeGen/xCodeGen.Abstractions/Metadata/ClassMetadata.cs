using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// 类名
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// 类的全限定名
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// 类中包含的方法元数据
        /// </summary>
        public ICollection<MethodMetadata> Methods { get; set; } = new Collection<MethodMetadata>();

        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// 来源类型（Class/Table等）
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 模板名称（用于代码生成）
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// 基类全限定名
        /// </summary>
        public string BaseType { get; set; } = string.Empty;

        /// <summary>
        /// 实现的接口全限定名列表
        /// </summary>
        public ICollection<string> ImplementedInterfaces { get; set; } = new Collection<string>();
    }
}