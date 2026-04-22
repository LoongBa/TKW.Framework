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

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintHeader();

        try
        {
            var configProvider = new ConfigurationProvider();
            var searchDir = Directory.GetCurrentDirectory();
            var config = configProvider.Load(searchDir) ?? throw new InvalidOperationException("无法加载配置文件。");

            var verbose = args.Any(a => a.Equals("-v", StringComparison.OrdinalIgnoreCase));
            var command = args.FirstOrDefault(a => !a.StartsWith("-"))?.ToLower() ?? "gen";

            return command switch
            {
                "help" => ShowHelp(),
                "init" => HandleInit(config),
                _ => await ExecuteWorkflow(config, verbose)
            };
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return 1;
        }
    }

    static async Task<int> ExecuteWorkflow(CodeGenConfig config, bool verbose)
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

    static async Task<GenerateResult> HandleGenerate(CodeGenConfig config, bool verbose)
    {
        var targetDll = ResolveAssemblyPath(config.TargetProject) ?? throw new FileNotFoundException("找不到程序集。请确认项目已编译。");
        var targetDir = Path.GetDirectoryName(Path.GetFullPath(targetDll))!;

        AssemblyLoadContext.Default.Resolving += (ctx, name) => {
            var p = Path.Combine(targetDir, name.Name + ".dll");
            return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
        };

        var assembly = Assembly.LoadFrom(targetDll);
        var contextType = GetLoadableTypes(assembly).FirstOrDefault(t =>
            typeof(IProjectMetaContext).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            ?? throw new InvalidOperationException("未发现元数据上下文。");

        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(contextType.TypeHandle);
        var contextInstance = typeof(ProjectMetaContextBase).GetProperty("Instance")?.GetValue(null) as IProjectMetaContext
            ?? throw new InvalidOperationException("元数据实例为空。");

        var templateEngine = new RazorLightTemplateEngine(config.TemplatesPath);
        var extractors = new List<IMetaDataExtractor> { new CompiledMetadataExtractor(contextInstance) };
        var engine = EngineFactory.Create(config, extractors, templateEngine, new FileSystemWriter());

        var result = await engine.GenerateAsync(new GenerateOptions { ProjectPath = config.TargetProject, OutputPath = config.OutputRoot, MetadataSources = new[] { "Code" } }, config);

        PrintSummary(config, result);
        if (verbose || result.Errors.Any()) PrintDetails(result, verbose);

        return result;
    }

    static async Task HandleInterfaceSynthesis(CodeGenConfig config, bool verbose)
    {
        Console.WriteLine("\n🔍 正在提取业务接口 (Interface Synthesis)...");
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

            var interfaceCode = synthesizer.Synthesize(className, ns, manualText, generatedText);

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
                    if (!string.IsNullOrEmpty(existingHash) && !string.IsNullOrEmpty(newHash) && existingHash == newHash)
                    {
                        writeFile = false;
                        if (verbose) Console.WriteLine($"  [-] 跳过未变更接口: I{className}Service");
                    }
                }
            }

            if (writeFile)
            {
                await File.WriteAllTextAsync(interfacePath, interfaceCode, Encoding.UTF8);
                if (verbose) Console.WriteLine($"  [+] 合成接口: I{className}Service -> {Path.GetRelativePath(config.OutputRoot, interfacePath)}");
                count++;
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✨ 接口提取完成，共生成/更新 {count} 个接口文件。");
        Console.ResetColor();
    }

    private static void PrintSummary(CodeGenConfig config, GenerateResult result)
    {
        Console.WriteLine($"📄 项目文件: {Path.GetFullPath(config.TargetProject)}");
        Console.WriteLine($"📂 输出目录: {Path.GetFullPath(config.OutputRoot)}");
        Console.WriteLine("--------------------------------------------------------------------");

        Console.Write("任务状态: ");
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("成功 ✨ ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("失败 ❌ ");
        }
        Console.ResetColor();

        Console.Write("增量策略: ");
        if (config.EnableSkipUnchanged)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("开启 (防抖) ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("关闭 (全量) ");
        }
        Console.ResetColor();

        Console.Write($"| 耗时: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{result.ElapsedMilliseconds}");
        Console.ResetColor();
        Console.WriteLine("ms");

        Console.Write("提取元数据：");
        Console.ForegroundColor = ConsoleColor.Cyan;
        var totalExtracted = result.ExtractedCounts.Values.Sum();
        Console.Write($"共 {totalExtracted} 项 ");
        Console.ResetColor();
        if (totalExtracted > 0)
            Console.Write($"({string.Join(", ", result.ExtractedCounts.Select(x => $"{x.Key}: {x.Value}"))})");
        else 
            Console.WriteLine();

        Console.Write($"| 生成/骨架: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{result.GeneratedFiles.Count}/{result.SkeletonFiles.Count} ");
        Console.ResetColor();

        Console.Write($"| 跳过: ");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"{result.SkippedCount} ");
        Console.ResetColor();
    }

    private static void PrintDetails(GenerateResult result, bool verbose)
    {
        var currentDir = Directory.GetCurrentDirectory();

        if (result.Errors.Any())
        {
            Console.WriteLine("\n❌ 错误详情列表:");
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  [X] {error}");
            }
            Console.ResetColor();
        }

        if (verbose)
        {
            // A. 生成产物明细
            if (result.GeneratedFiles.Any())
            {
                Console.WriteLine("\n🚀 生成清单 (Artifacts):");
                Console.ForegroundColor = ConsoleColor.Green;
                foreach (var file in result.GeneratedFiles)
                {
                    var relPath = Path.GetRelativePath(currentDir, file.Value);
                    Console.WriteLine($"  [+] {file.Key,-30} -> {relPath}");
                }
                Console.ResetColor();
            }

            // B. 骨架明细
            if (result.SkeletonFiles.Any())
            {
                Console.WriteLine("\n🏗️  初始化清单 (Skeletons):");
                Console.ForegroundColor = ConsoleColor.White;
                foreach (var file in result.SkeletonFiles)
                {
                    var relPath = Path.GetRelativePath(currentDir, file.Value);
                    Console.WriteLine($"  [#] {file.Key,-30} -> {relPath}");
                }
                Console.ResetColor();
            }

            // C. 跳过明细
            if (result.SkippedFiles.Any())
            {
                Console.WriteLine("\n⏭️  跳过清单 (Unchanged):");
                Console.ForegroundColor = ConsoleColor.Blue;
                foreach (var file in result.SkippedFiles)
                {
                    var relPath = Path.GetRelativePath(currentDir, file.Value);
                    Console.WriteLine($"  [-] {file.Key,-30} -> {relPath} (未变更)");
                }
                Console.ResetColor();
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }

    private static string? ResolveAssemblyPath(string projectPath)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
            var assemblyName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "AssemblyName")?.Value
                               ?? Path.GetFileNameWithoutExtension(projectPath);
            var binPath = Path.Combine(projectDir, "bin");
            return Directory.GetFiles(binPath, $"{assemblyName}.dll", SearchOption.AllDirectories)
                            .OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        }
        catch { return null; }
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("--------------------------------------------------------------------");
        Console.WriteLine(" xCodeGen Ver2.27 - 元数据驱动的代码生成工具 by LoongBa.cn 龙爸出品");
        Console.WriteLine("--------------------------------------------------------------------");
        Console.ResetColor();
    }

    private static void LogError(string msg) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"❌ 错误: {msg}"); Console.ResetColor(); }

    private static int ShowHelp()
    {
        Console.WriteLine("用法: xCodeGen [command] [options]");
        Console.WriteLine("命令: gen (默认) | init | help");
        Console.WriteLine("选项: -v, --verbose - 输出详细清单");
        return 0;
    }

    private static int HandleInit(CodeGenConfig config)
    {
        Console.WriteLine("✅ 配置文件加载成功。");
        Console.WriteLine($"目标项目: {Path.GetFullPath(config.TargetProject)}");
        return 0;
    }
}