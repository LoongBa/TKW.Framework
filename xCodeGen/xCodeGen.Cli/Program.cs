using TKW.Framework.Common.Extensions;
using xCodeGen.Core;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.Models;

namespace xCodeGen.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        PrintHeader();

        if (args.Length == 0 || args[0] == "help")
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();
        try
        {
            return command switch
            {
                "init" => HandleInit(),
                "gen" => await HandleGenerate(args),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 初始化默认配置文件
    /// </summary>
    static int HandleInit()
    {
        var config = new CodeGenConfig
        {
            TargetProject = "src/YourProject/YourProject.csproj",
            OutputRoot = "Generated",
            NamingRules =
            [
                new() { ArtifactType = "Dto", Pattern = "{Name}Dto" },
                new() { ArtifactType = "Validator", Pattern = "{Name}Validator" }
            ]
        };

        File.WriteAllText("xCodeGen.config.json", config.ToJson());
        Console.WriteLine("✅ 已在当前目录初始化 xCodeGen.config.json");
        return 0;
    }

    /// <summary>
    /// 执行生成逻辑，支持 --watch 模式
    /// </summary>
    static async Task<int> HandleGenerate(string[] args)
    {
        var watch = args.Contains("--watch");
        var configPath = "xCodeGen.config.json";

        // 1. 加载配置
        var config = CodeGenConfig.FromJson(await File.ReadAllTextAsync(configPath));

        // 2. 初始生成
        await ExecuteGenerate(config);

        if (watch)
        {
            Console.WriteLine("👀 正在监听文件变更 (按 Ctrl+C 退出)...");
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(Path.GetFullPath(config.TargetProject))!);
            watcher.Filter = "*.cs";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            // 监听逻辑：文件改变后防抖触发生成
            watcher.Changed += async (s, e) => {
                Console.WriteLine($"\n♻️ 检测到变更: {e.Name}，正在重新生成...");
                await ExecuteGenerate(config);
            };

            await Task.Delay(-1); // 阻塞进程以持续监听
        }

        return 0;
    }

    static async Task ExecuteGenerate(CodeGenConfig config)
    {
        // 使用工厂装配引擎
        // 注意：此处 Extractors 和 TemplateEngine 需要通过 DI 或手动初始化传入
        var engine = EngineFactory.Create(config, /* Extractors */ null, /* TemplateEngine */ null, /* FileWriter */ null);

        var options = new GenerateOptions
        {
            ProjectPath = config.TargetProject,
            OutputPath = config.OutputRoot,
            MetadataSources = ["Code"]
        };

        var result = await engine.GenerateAsync(options, config);
        Console.WriteLine(result.GetSummary());
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("xCodeGen - 元数据驱动的代码生成工具 [Ver 1.0]");
        Console.WriteLine("-------------------------------------------");
        Console.ResetColor();
    }

    private static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ 错误: {msg}");
        Console.ResetColor();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("用法:");
        Console.WriteLine("  xCodeGen init          - 在当前目录初始化配置文件");
        Console.WriteLine("  xCodeGen gen           - 根据配置执行代码生成");
        Console.WriteLine("  xCodeGen gen --watch   - 进入监听模式，实时同步变更");
    }

    private static int HandleUnknownCommand(string cmd)
    {
        LogError($"未知命令 '{cmd}'");
        return 1;
    }
}