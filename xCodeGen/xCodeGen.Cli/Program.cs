namespace xCodeGen.Cli;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("xCodeGen - 元数据驱动的代码生成工具");
        Console.WriteLine("-----------------------------------");
        return;
        try
        {

            // 获取配置文件路径（默认使用当前目录下的xcodegen.config.json）
            string configPath = args.Length > 0 ? args[0] : "xcodegen.config.json";
            Console.WriteLine($"使用配置文件: {configPath}");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("配置文件不存在", configPath);
            }

            Console.WriteLine("代码生成完成!");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"生成失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}