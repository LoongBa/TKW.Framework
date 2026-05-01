using RazorLight;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis; // 需要引用此命名空间
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Abstractions.Templates;

namespace xCodeGen.Core.Templates;

public class RazorLightTemplateEngine : ITemplateEngine, IDisposable
{
    private readonly RazorLightEngine _Engine;
    private bool _Disposed;
    public RazorLightTemplateEngine(string templateRootPath, string targetAssemblyPath)
    {
        if (!Directory.Exists(templateRootPath))
            throw new DirectoryNotFoundException($"模板目录不存在：{templateRootPath}");
        if (!File.Exists(targetAssemblyPath))
            throw new FileNotFoundException($"目标程序集不存在：{targetAssemblyPath}");

        // 核心修复逻辑：手动获取所有已加载程序集的引用路径，过滤掉无法定位的虚拟包
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
        
        // 添加目标程序集引用
        if (!string.IsNullOrEmpty(targetAssemblyPath) && File.Exists(targetAssemblyPath))
        {
            refs.Add(MetadataReference.CreateFromFile(targetAssemblyPath));
            // 建议：如果你能拿到目标 DLL 的依赖目录，也应该把旁边的依赖 DLL 加进去 
        }

        _Engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templateRootPath)
            .UseMemoryCachingProvider()
            // 1. 明确指定入口程序集
            .SetOperatingAssembly(Assembly.GetEntryAssembly())
            // 2. 注入手动提取的物理引用，跳过 DependencyContext 的自动扫描
            .AddMetadataReferences(refs.ToArray())
            .Build();
    }

    public void LoadTemplates(string templatePath)
    {
        if (!Directory.Exists(templatePath))
            throw new DirectoryNotFoundException($"模板路径不存在：{templatePath}");
    }

    /// <inheritdoc/>>
    public async Task<string> RenderAsync(ClassMetadata metadata, string templateName)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentNullException(nameof(templateName));

        // 渲染模板
        return await _Engine.CompileRenderAsync(templateName, metadata);
    }

    /// <inheritdoc/>>
    public async Task<string> RenderAsync(IProjectMetaContext project, string templateName)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentNullException(nameof(templateName));

        // 渲染模板
        return await _Engine.CompileRenderAsync(templateName, project);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_Disposed) return;
        _Disposed = true;
    }
}