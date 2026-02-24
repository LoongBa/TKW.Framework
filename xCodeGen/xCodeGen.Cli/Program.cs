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
        // 核心修复：强制 UTF8 编码以支持 Emojis 和 Nerd Font 符号
        Console.OutputEncoding = Encoding.UTF8;

        PrintHeader();

        try
        {
            var configProvider = new ConfigurationProvider();
            var searchDir = Directory.GetCurrentDirectory();
            if (!File.Exists(Path.Combine(searchDir, "xCodeGen.config.json")))
            {
                searchDir = AppContext.BaseDirectory;
            }

            var config = configProvider.Load(searchDir);
            if (config == null)
            {
                LogError("无法加载配置文件 xCodeGen.config.json。");
                return 1;
            }

            // 解析详细日志开关 (-v 或 --verbose)
            var verbose = args.Any(a => a.Equals("-v", StringComparison.OrdinalIgnoreCase) || a.Equals("--verbose", StringComparison.OrdinalIgnoreCase));

            // 提取非选项参数作为命令，默认为 gen
            var command = args.FirstOrDefault(a => !a.StartsWith("-"))?.ToLower() ?? "gen";

            return command switch
            {
                "help" => ShowHelp(),
                "init" => HandleInit(config),
                _ => await HandleGenerate(config, verbose)
            };
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return 1;
        }
    }

    static async Task<int> HandleGenerate(CodeGenConfig config, bool verbose)
    {
        if (string.IsNullOrWhiteSpace(config.TargetProject))
            throw new InvalidOperationException("未配置 TargetProject 路径。");

        var targetDll = ResolveAssemblyPath(config.TargetProject);
        if (string.IsNullOrEmpty(targetDll) || !File.Exists(targetDll))
            throw new FileNotFoundException($"找不到程序集。请确认项目文件已成功编译。");

        var targetDir = Path.GetDirectoryName(Path.GetFullPath(targetDll))!;

        // 注册依赖解析钩子以解决加载问题
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var expectedPath = Path.Combine(targetDir, assemblyName.Name + ".dll");
            return File.Exists(expectedPath) ? context.LoadFromAssemblyPath(expectedPath) : null;
        };

        var assembly = Assembly.LoadFrom(targetDll);
        var contextType = GetLoadableTypes(assembly).FirstOrDefault(t =>
            typeof(IProjectMetaContext).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

        if (contextType == null)
            throw new InvalidOperationException("程序集中未发现有效的 IProjectMetaContext 实现。");

        var instanceProperty = contextType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var contextInstance = instanceProperty?.GetValue(null) as IProjectMetaContext;

        if (contextInstance == null)
            throw new InvalidOperationException("无法获取元数据上下文实例。");

        // 初始化核心组件
        var fileWriter = new FileSystemWriter();
        var templateEngine = new RazorLightTemplateEngine(config.TemplatesPath);
        var extractors = new List<IMetaDataExtractor> { new CompiledMetadataExtractor(contextInstance) };
        var engine = EngineFactory.Create(config, extractors, templateEngine, fileWriter);

        var options = new GenerateOptions
        {
            ProjectPath = config.TargetProject,
            OutputPath = config.OutputRoot,
            MetadataSources = new List<string> { "Code" }
        };

        var result = await engine.GenerateAsync(options, config);

        // --- 输出统计摘要 ---
        PrintSummary(config, result);

        // --- 输出详细清单 (Verbose 模式或有错误时) ---
        if (verbose || result.Errors.Any())
        {
            PrintDetails(result, verbose);
        }

        return result.Success ? 0 : 1;
    }

    private static void PrintSummary(CodeGenConfig config, GenerateResult result)
    {
        Console.WriteLine("\n-------------------------------------------");
        Console.WriteLine($"📄 项目文件: {Path.GetFullPath(config.TargetProject)}");
        Console.WriteLine($"📂 输出目录: {Path.GetFullPath(config.OutputRoot)}");
        Console.WriteLine("-------------------------------------------");

        Console.Write("提取元数据: ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        var totalExtracted = result.ExtractedCounts.Values.Sum();
        Console.Write($"共 {totalExtracted} 条 ");
        Console.ResetColor();
        if (totalExtracted > 0)
        {
            Console.WriteLine($"({string.Join(", ", result.ExtractedCounts.Select(x => $"{x.Key}: {x.Value}"))})");
        }
        else { Console.WriteLine(); }

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

        Console.Write("| 增量策略: ");
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

        Console.Write($"| 生成/骨架: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{result.GeneratedFiles.Count}/{result.SkeletonFiles.Count} ");
        Console.ResetColor();

        Console.Write($"| 跳过: ");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{result.SkippedCount} ");
        Console.ResetColor();

        Console.WriteLine($"| 耗时: {result.ElapsedMilliseconds}ms");
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
        Console.WriteLine("xCodeGen - 元数据驱动的代码生成工具 [Ver 2.25]");
        Console.WriteLine("-------------------------------------------");
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