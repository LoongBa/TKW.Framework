using System;

namespace xCodeGen.Abstractions.Attributes
{
    /// <summary>
    /// 标记需要生成代码的元数据配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    public class GenerateCodeAttribute : Attribute
    {
        public static string TypeFullName => typeof(GenerateCodeAttribute).FullName;

        /// <summary> 是否为只读视图 [New] </summary>
        public bool IsView { get; set; } = false;

        /// <summary> 业务用户标识类型（如 DmpUserInfo），必填 </summary>
        public string BaseUserType { get; set; } = string.Empty;

        /// <summary> 默认分页大小 </summary>
        public int DefaultPageSize { get; set; } = 20;

        /// <summary> 默认全量限制 </summary>
        public int DefaultLimit { get; set; } = 100;

        /// <summary> 搜索结果上限 </summary>
        public int MaxSearchLimit { get; set; } = 100;

        /// <summary> 用于覆盖类元数据的命名空间 </summary>
        public string Namespace { get; set; } = string.Empty;
    }
}