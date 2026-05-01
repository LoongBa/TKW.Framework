using xCodeGen.Core.Services;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace xCodeGen.Cli;

// 隔离的 TUI 部分
partial class Program
{
    static async Task<int> RunTuiModeAsync(string? initialConfigPath, bool verbose)
    {
        // 1. 初始化终端应用状态（核心控制 + 业务状态）
        State<bool> exit = new(false);
        State<string> statusText = new("就绪");
        State<string?> selectedConfigPath = new(initialConfigPath);
        State<List<string>> configHistory = new(LoadHistory());

        // 日志控件（终端实时输出）
        var logControl = new LogControl { MaxCapacity = 500 }.WrapText(true);
        logControl.AppendMarkupLine("[success]xCodeGen TUI 启动成功！[/]");
        if (!string.IsNullOrEmpty(initialConfigPath))
        {
            logControl.AppendLine($"已加载配置：{initialConfigPath}");
        }

        // 2. 构建【严格匹配需求】的 TUI 布局
        var rootLayout = new VStack
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            Spacing = 1
        };

        // ==============================================
        // 主内容区：Grid 双栏布局（左侧30% | 右侧70%）
        // ==============================================
        var mainGrid = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(3) },  // 左侧 30%
                new ColumnDefinition { Width = GridLength.Star(7) }   // 右侧 70%
            )
            .ColumnGap(2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        // ------------------------------
        // 左侧面板：30% - 项目配置区域
        // ------------------------------
        var listBox = new ListBox<string>()
            .Items(configHistory.Value)
            .HorizontalAlignment(Align.Stretch)
            .MinHeight(8)
            /*.Items((_, e) =>
            {
                selectedConfigPath.Value = e.Item;
                statusText.Value = $"选中配置：{e.Item}";
                logControl.AppendLine($"切换配置：{e.Item}");
            })*/;
        var leftPanel = new Group()
            .TopLeftText("项目配置")
            .Padding(1)
            .Content(new VStack(
                // 配置列表
                listBox,
                // 浏览配置按钮
                new Button("浏览配置文件")
                    .Tone(ControlTone.Primary)
                    .HorizontalAlignment(Align.Stretch)
                    .Click(() =>
                    {
                        statusText.Value = "打开文件浏览（可扩展实现）";
                        logControl.AppendMarkupLine("[info]触发配置文件浏览操作[/]");
                    }),
                // 配置编辑区
                new TextArea()
                    .Placeholder("编辑配置内容...")
                    .HorizontalAlignment(Align.Stretch)
                    .VerticalAlignment(Align.Stretch)
                    .Text(selectedConfigPath)
            ).Spacing(1))
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        // ------------------------------
        // 右侧面板：70% - 仪表盘+日志+按钮
        // ------------------------------
        var rightPanel = new VStack
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            Spacing = 1
        };

        // 仪表盘面板
        var dashboardPanel = new Group()
            .TopLeftText("仪表盘")
            .Padding(1)
            .Content(new VStack(
                new TextBlock(() => $"当前配置：{selectedConfigPath.Value ?? "未选择"}"),
                new TextBlock(() => $"历史配置数：{configHistory.Value.Count}"),
                new TextBlock(() => $"运行状态：{statusText.Value}")
            ).Spacing(1))
            .HorizontalAlignment(Align.Stretch);

        // 日志面板
        var logPanel = new Group()
            .TopLeftText("运行日志")
            .Padding(1)
            .Content(logControl)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        // 功能按钮栏（HStack 水平排列）
        var buttonBar = new HStack(
            new Button("生成代码").Tone(ControlTone.Success),
            new Button("验证配置").Tone(ControlTone.Warning),
            new Button("清空日志").Click(() => logControl.Clear()),
            new Button("退出").Tone(ControlTone.Error).Click(() => exit.Value = true)
        )
        .Spacing(2)
        .HorizontalAlignment(Align.Center);

        // 组装右侧面板
        rightPanel.Children.Add(dashboardPanel);
        rightPanel.Children.Add(logPanel);
        rightPanel.Children.Add(buttonBar);

        // 将左右面板加入 Grid
        mainGrid.Cell(leftPanel, 0, 0);
        mainGrid.Cell(rightPanel, 0, 1);

        // ==============================================
        // 底部状态栏（HStack 水平布局）
        // ==============================================
        var statusBar = new HStack(
            new TextBlock(() => $"状态：{statusText.Value}"),
            //new Spacer(),
            new TextBlock("xCodeGen CLI | 终端UI界面")
        )
        //.Padding(1)
        .Spacing(1)
        .HorizontalAlignment(Align.Stretch)
        .Style(BorderStyle.Rounded);

        // ==============================================
        // 组装根布局
        // ==============================================
        rootLayout.Children.Add(mainGrid);
        rootLayout.Children.Add(statusBar);

        // 3. 运行终端UI（异步，保留你的退出逻辑）
        await Terminal.RunAsync(
            rootLayout,
            onUpdate: () => exit.Value
                ? TerminalLoopResult.StopAndKeepVisual
                : TerminalLoopResult.Continue
        );

        // 4. 保留你原有的配置/历史记录业务逻辑
        var history = LoadHistory();
        var currentConfig = string.IsNullOrEmpty(initialConfigPath)
            ? null
            : new ConfigurationProvider().Load(null, initialConfigPath);

        logControl.AppendMarkupLine("[dim]程序已退出[/]");
        return 0;
    }

    // 保留你的原有方法
    private static List<string> LoadHistory() { /* ... */ return new List<string>(); }
}