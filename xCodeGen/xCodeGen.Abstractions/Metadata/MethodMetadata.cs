using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Abstractions.Metadata
{
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
        public string Summary { get; set; } = string.Empty;
        /// <summary>
        /// 是否为异步方法
        /// </summary>
        public bool IsAsync { get; set; }
        /// <summary>
        /// 来源文件路径
        /// </summary>
        public string SourceFile { get; set; } = string.Empty;
        /// <summary>
        /// 方法来源
        /// </summary>
        public MethodOrigin Origin { get; set; }

        /// <summary>
        /// 方法参数元数据
        /// </summary>
        public List<ParameterMetadata> Parameters { get; set; } = new List<ParameterMetadata>();
        /// <summary> 方法级特性 </summary>
        public List<AttributeMetadata> Attributes { get; set; } = new List<AttributeMetadata>();
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
        public bool IsStatic { get; set; }
        /// <summary>
        /// 访问修饰符
        /// </summary>
        public string AccessModifier { get; set; } = "public";
    }
    /// <summary>
    /// 方法来源
    /// </summary>
    public enum MethodOrigin
    {
        /// <summary>
        /// 业务方法：
        /// </summary>
        Custom = 0,
        /// <summary>
        /// 框架方法：实现 IEntityDAC 的方法（无论手写还是生成）
        /// </summary>
        Infrastructure = 1,
        /// <summary>
        /// 生成的方法：模板生成的.g.cs 中的方法（AI 仅作为索引）
        /// </summary>
        Generated = 2,
    }
}