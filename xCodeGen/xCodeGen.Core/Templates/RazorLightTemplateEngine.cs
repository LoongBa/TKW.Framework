using System;
using System.IO;
using System.Threading.Tasks;
using RazorLight;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Abstractions.Templates;

namespace xCodeGen.Core.Templates;

/// <summary>
/// 基于RazorLight的模板引擎实现
/// </summary>
public class RazorLightTemplateEngine : ITemplateEngine, IDisposable
{
    private readonly RazorLightEngine _Engine;
    private bool _Disposed;

    public RazorLightTemplateEngine(string templateRootPath)
    {
        if (!Directory.Exists(templateRootPath))
            throw new DirectoryNotFoundException($"模板目录不存在：{templateRootPath}");

        _Engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templateRootPath)
            .UseMemoryCachingProvider()
            // 注意：将 HTML 编码器设置为 Null (不转义)
            .AddDefaultNamespaces("System.Text.Encodings.Web")
            .Build();
    }

    public void LoadTemplates(string templatePath)
    {
        // RazorLight会动态加载模板，此处仅验证路径
        if (!Directory.Exists(templatePath))
            throw new DirectoryNotFoundException($"模板路径不存在：{templatePath}");
    }

    public async Task<string> RenderAsync(ClassMetadata metadata, string templateName)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentNullException(nameof(templateName));

        // 渲染模板（模板文件需放在模板根目录，如"ClassTemplate.cshtml"）
        return await _Engine.CompileRenderAsync(templateName, metadata);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_Disposed) return;
        if (disposing)
        {
            // 释放托管资源
        }
        _Disposed = true;
    }
}