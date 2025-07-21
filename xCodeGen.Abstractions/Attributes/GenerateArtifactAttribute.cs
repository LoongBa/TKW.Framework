using System;

namespace xCodeGen.Abstractions.Attributes
{
    /// <summary>
    /// 标记需要生成代码产物的类或方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class GenerateArtifactAttribute : Attribute
    {
        /// <summary>
        /// 产物类型（用于匹配模板）
        /// </summary>
        public string ArtifactType { get; set; }

        /// <summary>
        /// 模板名称（从配置中查找对应模板）
        /// </summary>
        public string TemplateName { get; set; } = "Default";

        /// <summary>
        /// 是否覆盖已有文件
        /// </summary>
        public bool Overwrite { get; set; } = false;
    }
}
    