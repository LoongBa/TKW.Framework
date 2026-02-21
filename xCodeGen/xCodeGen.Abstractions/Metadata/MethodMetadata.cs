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
        /// 方法参数元数据
        /// </summary>
        public List<ParameterMetadata> Parameters { get; set; } = [];

        /// <summary>
        /// 方法的唯一签名（用于处理重载）
        /// </summary>
        public string UniqueSignature
        {
            get
            {
                var paramTypes = string.Join("_", Parameters.Select(p => p.TypeName.Replace("?", "Nullable").Replace("<", "_").Replace(">", "_")));
                return $"{Name}_{paramTypes}";
            }
        }
        public bool IsStatic { get; set; }
        /// <summary>
        /// 访问修饰符
        /// </summary>
        public string AccessModifier { get; set; } = "public";
    }
}