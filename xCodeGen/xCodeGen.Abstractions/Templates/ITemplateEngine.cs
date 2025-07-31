using System.Threading.Tasks;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Abstractions.Templates
{
    /// <summary>
    /// 模板引擎接口
    /// </summary>
    public interface ITemplateEngine
    {
        /// <summary>
        /// 加载模板
        /// </summary>
        /// <param name="templatePath">模板路径</param>
        void LoadTemplates(string templatePath);

        /// <summary>
        /// 渲染模板
        /// </summary>
        /// <param name="metadata">元数据</param>
        /// <param name="templateName">模板名称</param>
        /// <returns>渲染后的代码</returns>
        Task<string> RenderAsync(ClassMetadata metadata, string templateName);
    }
}
