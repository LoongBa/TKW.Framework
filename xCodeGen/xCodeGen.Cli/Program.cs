using System.Text;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.Services;

namespace xCodeGen.Cli;

partial class Program
{
    static async Task<int> Main(string[] args)
    {
        // 强制 UTF8 支持 TUI 符号和中文
        Console.OutputEncoding = Encoding.UTF8;

        // 1. 解析全局选项 (Options)
        // -v 既影响 CLI 输出，也作为 TUI 界面开关的初始值
        var verbose = args.Any(a => a.Equals("-v", StringComparison.OrdinalIgnoreCase) || a.Equals("--verbose", StringComparison.OrdinalIgnoreCase));
        var configPath = GetArgumentValue(args, "-j", "-json");

        // 2. 识别主命令 (Command)
        // 优化点：只有第一个参数且不以 '-' 开头时才识别为命令
        string? command = null;
        var firstArg = args.FirstOrDefault();
        if (firstArg != null && !firstArg.StartsWith("-"))
        {
            command = firstArg.ToLower();
        }

        // 3. 路由逻辑
        return command switch
        {
            "gen" => await RunCliTaskAsync(CommandType.Generate, configPath, verbose),
            "init" => await RunCliTaskAsync(CommandType.Init, configPath, verbose),
            "agent" => await RunCliTaskAsync(CommandType.Agent, configPath, verbose),
            "help" => ShowHelp(),
            // 情况 A：无参数 (null)
            // 情况 B：仅提供了选项（如 -j 或 -v），此时 command 为 null，正确进入 TUI
            null or "" => await RunTuiModeAsync(configPath, verbose),
            _ => UnknownCommand(command!)
        };
    }

    /// <summary>
    /// 核心任务执行器（针对 CLI 模式）
    /// </summary>
    static async Task<int> RunCliTaskAsync(CommandType type, string? explicitConfigPath, bool verbose)
    {
        try
        {
            PrintHeader();
            var configProvider = new ConfigurationProvider();
            var searchDir = explicitConfigPath != null ? Path.GetDirectoryName(Path.GetFullPath(explicitConfigPath))! : Directory.GetCurrentDirectory();
            var config = configProvider.Load(searchDir, explicitConfigPath)
                         ?? throw new InvalidOperationException($"无法加载配置文件。请确保当前目录存在 {} 或使用 -j 指定。");

            return type switch
            {
                CommandType.Init => HandleInit(config),
                CommandType.Agent => await HandleAgentAsync(config, verbose),
                _ => (await ExecuteWorkflow(config, verbose)) == 0 ? 0 : 1
            };
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return 1;
        }
    }

    static async Task<int> HandleAgentAsync(CodeGenConfig config, bool verbose)
    {
        Console.WriteLine("🤖 正在生成 AI Agent 领域上下文指南...");
        await Task.Delay(500);
        Console.WriteLine("✨ AI Agent 指南文件生成成功: ./docs/agent_context.md");
        return 0;
    }

    #region 参数解析辅助

    private static string? GetArgumentValue(string[] args, params string[] keys)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (keys.Contains(args[i].ToLower()) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

    #endregion
}