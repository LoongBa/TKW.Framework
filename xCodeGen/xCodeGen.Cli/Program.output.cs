using xCodeGen.Core.Configuration;
using xCodeGen.Core.Models;

namespace xCodeGen.Cli;

partial class Program
{
    private const string Version = " xCodeGen V3.0 - 元数据驱动的代码生成工具 by LoongBa.cn 龙爸出品";

    private static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ 错误: {msg}");
        Console.ResetColor();
    }
    
    private static int ShowHelp()
    {
        Console.WriteLine($"\n{Version}\n");
        Console.WriteLine("用法:");
        Console.WriteLine("  xCodeGen [command] [options]");
        Console.WriteLine("\n命令:");
        Console.WriteLine("  (empty)         进入 TUI 交互界面，命令参数值作为默认值");
        Console.WriteLine("  gen             执行代码生成 (Service, Controllers 等)");
        Console.WriteLine("  init            初始化项目脚手架");
        Console.WriteLine("  agent           生成 AI Agent 所需的领域指南文档");
        Console.WriteLine("\n选项:");
        Console.WriteLine("  -j, -json [path]  指定配置文件路径");
        Console.WriteLine("  -v, --verbose     显示执行明细");
        return 0;
    }

    private static int UnknownCommand(string cmd)
    {
        LogError($"未知命令: {cmd}");
        return ShowHelp();
    }

    private static int HandleInit(CodeGenConfig config)
    {
        Console.WriteLine("✅ 配置文件加载成功。");
        Console.WriteLine($"目标项目: {Path.GetFullPath(config.TargetProject)}");
        return 0;
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("--------------------------------------------------------------------");
        Console.WriteLine($" {Version}");
        Console.WriteLine("--------------------------------------------------------------------");
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
}