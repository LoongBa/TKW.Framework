using System.Reflection;
using System.Runtime.Loader;
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
        PrintHeader();

        try
        {
            var configProvider = new ConfigurationProvider();

            // 核心改进：优先从命令行执行的当前目录查找配置，实现“配置随项目走”
            var searchDir = Directory.GetCurrentDirectory();
            if (!File.Exists(Path.Combine(searchDir, "xCodeGen.config.json")))
            {
                // 如果当前目录没有，回退到 exe 所在目录
                searchDir = AppContext.BaseDirectory;
            }

            var config = configProvider.Load(searchDir);

            if (config == null)
            {
                LogError("无法加载配置文件 xCodeGen.config.json。");
                return 1;
            }

            var command = args.Length > 0 ? args[0].ToLower() : "gen";

            return command switch
            {
                "gen" => await HandleGenerate(config),
                "help" => ShowHelp(),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return 1;
        }
    }

    static async Task<int> HandleGenerate(CodeGenConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TargetProject))
            throw new InvalidOperationException("未配置 TargetProject 路径。");

        // 1. 定位目标 DLL
        var targetDll = ResolveAssemblyPath(config.TargetProject);
        if (string.IsNullOrEmpty(targetDll) || !File.Exists(targetDll))
            throw new FileNotFoundException($"找不到程序集。请确认项目已成功编译: {config.TargetProject}");

        var targetDir = Path.GetDirectoryName(Path.GetFullPath(targetDll))!;
        Console.WriteLine($"📦 目标程序集: {Path.GetFileName(targetDll)}");
        Console.WriteLine($"🔍 依赖搜索路径: {targetDir}");

        // 2. 核心修复：设置动态依赖解析钩子，解决 Autofac/FreeSql 等程序集加载失败问题
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var expectedPath = Path.Combine(targetDir, assemblyName.Name + ".dll");
            if (File.Exists(expectedPath))
            {
                return context.LoadFromAssemblyPath(expectedPath);
            }
            return null;
        };

        // 3. 安全加载程序集并提取上下文
        var assembly = Assembly.LoadFrom(targetDll);

        // 使用防御性加载，避免因部分依赖缺失导致 GetTypes 崩溃
        var contextType = GetLoadableTypes(assembly).FirstOrDefault(t =>
            typeof(IProjectMetaContext).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (contextType == null)
            throw new InvalidOperationException("程序集中未发现有效的 IProjectMetaContext 实现。");

        var instanceProperty = contextType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty == null)
            throw new InvalidOperationException($"类型 {contextType.Name} 缺少公共静态 Instance 属性。");

        var contextInstance = instanceProperty.GetValue(null) as IProjectMetaContext;
        if (contextInstance == null)
            throw new InvalidOperationException("无法获取元数据上下文实例。");

        // 4. 初始化引擎组件
        var fileWriter = new FileSystemWriter();
        if (string.IsNullOrWhiteSpace(config.TemplatesPath))
            throw new InvalidOperationException("TemplatesPath 未配置。");

        var templateEngine = new RazorLightTemplateEngine(config.TemplatesPath);

        var extractors = new List<IMetaDataExtractor> { new CompiledMetadataExtractor(contextInstance) };
        var engine = EngineFactory.Create(config, extractors, templateEngine, fileWriter);

        // 5. 执行生成
        var options = new GenerateOptions
        {
            ProjectPath = config.TargetProject,
            OutputPath = config.OutputRoot,
            MetadataSources = new List<string> { "Code" }
        };

        var result = await engine.GenerateAsync(options, config);

        // 6. 输出结果摘要
        Console.WriteLine(result.GetSummary());
        if (result.Errors.Any())
        {
            Console.WriteLine("\n❌ 详细错误列表:");
            foreach (var error in result.Errors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[-] {error}");
                Console.ResetColor();
            }
        }

        return result.Success ? 0 : 1;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null)!;
        }
    }

    private static string? ResolveAssemblyPath(string projectPath)
    {
        if (!File.Exists(projectPath)) return null;
        try
        {
            var doc = XDocument.Load(projectPath);
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            if (projectDir == null) return null;

            var assemblyName = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "AssemblyName")?.Value
                               ?? Path.GetFileNameWithoutExtension(projectPath);

            var binPath = Path.Combine(projectDir, "bin");
            if (!Directory.Exists(binPath)) return null;

            return Directory.GetFiles(binPath, $"{assemblyName}.dll", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("xCodeGen - 元数据驱动的代码生成工具 [Ver 2.2]");
        Console.WriteLine("-------------------------------------------");
        Console.ResetColor();
    }

    private static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ 错误: {msg}");
        Console.ResetColor();
    }

    private static int ShowHelp()
    {
        Console.WriteLine("用法: xcodegen gen [参数]");
        Console.WriteLine("说明: 默认读取当前目录下的 xCodeGen.config.json 执行代码生成。");
        return 0;
    }

    private static int HandleUnknownCommand(string cmd)
    {
        LogError($"未知命令: {cmd}");
        return 1;
    }
}