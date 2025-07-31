using System;

namespace xCodeGen.Abstractions.Attributes
{
    /// <summary>
    /// 标记需要生成元数据的类或接口
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    public class GenerateCodeAttribute : Attribute
    {
        public static string TypeFullName => typeof(GenerateCodeAttribute).FullName;

        /// <summary>
        /// 生成模式（Full/Lite）
        /// </summary>
        public string Mode { get; set; } = "Full";

        /// <summary>
        /// 是否包含详细注释
        /// </summary>
        public bool IncludeComments { get; set; } = true;

        /// <summary>
        /// 是否覆盖已有文件
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// 目标语言，例如 Python、C#、Java 等
        /// </summary>
        public string TargetLanguage { get; set; } = "C#";

        /// <summary>
        /// 生成类型（以便模板区分不同类型，如 Dto、Entity、Controller 等）
        /// </summary>
        public string Type { get; set; } = "Dto";

        /// <summary>
        /// 模板名称（从配置中查找对应模板，默认按规则匹配对应类型的模板，这里可以覆盖匹配规则）
        /// </summary>
        public string TemplateName { get; set; } = "";

    }
}
