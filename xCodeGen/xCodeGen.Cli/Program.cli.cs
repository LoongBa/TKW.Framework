using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Xml.Linq;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.Extraction;
using xCodeGen.Core.IO;
using xCodeGen.Core.Models;
using xCodeGen.Core.Services;
using xCodeGen.Core.Templates;

namespace xCodeGen.Cli;

/// <summary>
/// CLI 运行的任务类型
/// </summary>
public enum CommandType
{
    /// <summary>执行代码生成</summary>
    Generate,
    /// <summary>初始化项目脚手架</summary>
    Init,
    /// <summary>生成 AI Agent 指南文件</summary>
    Agent
}

partial class Program
{
    /// <summary>
    /// 获取程序集中所有可加载的类型。
    /// </summary>
    /// <param name="assembly">要检查的程序集。</param>
    /// <returns>程序集中可加载的类型集合。如果发生类型加载异常，则返回成功加载的类型。</returns>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null)!;
        }
    }

    /// <summary>
    /// 执行完整的工作流，包括标准代码生成和可选的接口合成。
    /// </summary>
    /// <param name="config">代码生成配置对象，包含输入输出路径、模板路径等设置。</param>
    /// <param name="verbose">是否输出详细的日志信息。</param>
    /// <returns>表示执行状态的整数，0 表示成功，非 0 表示失败。</returns>
    private static async Task<int> ExecuteWorkflow(CodeGenConfig config, bool verbose)
    {
        // 1. 执行标准代码生成 (Service.g.cs, Service.cs 等)
        var result = await HandleGenerate(config, verbose);
        if (!result.Success) return 1;

        // 2. 执行接口合成 (V4 AOP 核心准备)
        if (config.EnableInterfaceExtraction)
        {
            await HandleInterfaceSynthesis(config, verbose);
        }

        return 0;
    }

    /// <summary>
    /// 处理核心代码生成逻辑，包括加载目标程序集、提取元数据并使用模板引擎生成代码。
    /// </summary>
    /// <param name="config">代码生成配置对象。</param>
    /// <param name="verbose">是否输出详细的日志信息。</param>
    /// <returns>包含生成结果详情的 <see cref="GenerateResult"/> 对象。</returns>
    /// <exception cref="FileNotFoundException">当找不到目标程序集时抛出。</exception>
    /// <exception cref="InvalidOperationException">当未发现元数据上下文或实例为空时抛出。</exception>
    private static async Task<GenerateResult> HandleGenerate(CodeGenConfig config, bool verbose)
    {
        var targetDll = ResolveAssemblyPath(config.TargetProject) ??
                        throw new FileNotFoundException("找不到程序集。请确认项目已编译。");
        var targetDir = Path.GetDirectoryName(Path.GetFullPath(targetDll))!;

        // 设置自定义程序集解析逻辑，确保依赖项能被正确加载
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var p = Path.Combine(targetDir, name.Name + ".dll");
            return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
        };

        var assembly = Assembly.LoadFrom(targetDll);
        
        // 查找实现了 IProjectMetaContext 的具体类型
        var contextType = GetLoadableTypes(assembly).FirstOrDefault(t =>
                              typeof(IProjectMetaContext).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                          ?? throw new InvalidOperationException("未发现元数据上下文。");

        // 强制执行静态构造函数以初始化上下文单例
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(contextType.TypeHandle);
        var contextInstance =
            typeof(ProjectMetaContextBase).GetProperty("Instance")?.GetValue(null) as IProjectMetaContext
            ?? throw new InvalidOperationException("元数据实例为空。");

        var templateEngine = new RazorLightTemplateEngine(config.TemplatesPath);
        var extractors = new List<IMetaDataExtractor> { new CompiledMetadataExtractor(contextInstance) };
        var engine = EngineFactory.Create(config, extractors, templateEngine, new FileSystemWriter());

        var result = await engine.GenerateAsync(
            new GenerateOptions
            {
                ProjectPath = config.TargetProject, OutputPath = config.OutputRoot, MetadataSources = new[] { "Code" }
            }, config);

        PrintSummary(config, result);
        if (verbose || result.Errors.Any()) PrintDetails(result, verbose);

        return result;
    }

    /// <summary>
    /// 处理接口合成逻辑。通过分析手动编写的 Service 类和生成的 .g.cs 文件，提取并合成接口文件。
    /// </summary>
    /// <param name="config">代码生成配置对象，包含输出路径和特性白名单等设置。</param>
    /// <param name="verbose">是否输出详细的日志信息。</param>
    private static async Task HandleInterfaceSynthesis(CodeGenConfig config, bool verbose)
    {
        Console.WriteLine("\n🔍 正在提取业务接口...");
        var synthesizer = new InterfaceSynthesizer();
        var serviceDir = Path.Combine(config.OutputRoot, config.ServiceDirectory);
        var interfaceDir = Path.Combine(config.OutputRoot, config.InterfaceOutputPath);

        if (!Directory.Exists(serviceDir)) return;
        if (!Directory.Exists(interfaceDir)) Directory.CreateDirectory(interfaceDir);

        var serviceFiles = Directory.GetFiles(serviceDir, "*Service.cs");
        int count = 0;

        foreach (var manualFile in serviceFiles)
        {
            var className = Path.GetFileNameWithoutExtension(manualFile).Replace("Service", "");
            var generatedFile = manualFile.Replace(".cs", ".g.cs");

            if (!File.Exists(generatedFile)) continue;

            var manualText = await File.ReadAllTextAsync(manualFile);
            var generatedText = await File.ReadAllTextAsync(generatedFile);

            var nsLine = generatedText.Split('\n').FirstOrDefault(l => l.Trim().StartsWith("namespace")) ?? "";
            var ns = nsLine.Replace("namespace", "").Replace(";", "").Trim();

            // 传入配置中的 AttributeWhitelist（可能为空 -> 表示复制所有特性）
            var attributeWhitelist = (config.AttributeWhitelist != null && config.AttributeWhitelist.Count > 0)
                ? config.AttributeWhitelist.AsEnumerable()
                : null;

            var interfaceCode = synthesizer.Synthesize(className, ns, manualText, generatedText, attributeWhitelist);

            var interfacePath = Path.Combine(interfaceDir, $"I{className}Service.g.cs");

            // 从生成的 interfaceCode 中解析新 hash
            var newHash = "";
            {
                var m = System.Text.RegularExpressions.Regex.Match(interfaceCode, @"xCodeGen\.Hash:\s*([0-9a-fA-F]+)");
                if (m.Success) newHash = m.Groups[1].Value;
            }

            bool writeFile = true;
            if (File.Exists(interfacePath))
            {
                var existing = await File.ReadAllTextAsync(interfacePath);
                var m = System.Text.RegularExpressions.Regex.Match(existing, @"xCodeGen\.Hash:\s*([0-9a-fA-F]+)");
                if (m.Success)
                {
                    var existingHash = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(existingHash) && !string.IsNullOrEmpty(newHash) &&
                        existingHash == newHash)
                    {
                        writeFile = false;
                        if (verbose) Console.WriteLine($"  [-] 跳过未变更接口: I{className}Service");
                    }
                }
            }

            if (writeFile)
            {
                await File.WriteAllTextAsync(interfacePath, interfaceCode, Encoding.UTF8);
                if (verbose)
                    Console.WriteLine(
                        $"  [+] 合成接口: I{className}Service -> {Path.GetRelativePath(config.OutputRoot, interfacePath)}");
                count++;
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✨ 接口提取完成，共生成/更新 {count} 个接口文件。");
        Console.ResetColor();
    }

    /// <summary>
    /// 解析项目路径，查找并返回对应的编译后程序集（DLL）路径。
    /// </summary>
    /// <param name="projectPath">项目文件（.csproj）的路径。</param>
    /// <returns>找到的 DLL 文件的完整路径；如果未找到则返回 null。</returns>
    private static string? ResolveAssemblyPath(string projectPath)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
            // 尝试读取 XML 中的 AssemblyName，如果没有则使用文件名
            var assemblyName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "AssemblyName")?.Value
                               ?? Path.GetFileNameWithoutExtension(projectPath);
            var binPath = Path.Combine(projectDir, "bin");
            // 在 bin 目录下递归查找匹配的 DLL，并按最后写入时间降序排列（取最新的）
            return Directory.GetFiles(binPath, $"{assemblyName}.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
