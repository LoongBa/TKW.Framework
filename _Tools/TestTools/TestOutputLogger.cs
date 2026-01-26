using Microsoft.Extensions.Logging;
using Xunit;

namespace TKWF.Tools.TestTools;

/// <summary>
/// 自定义测试日志类：将 ILogger 日志转发到 ITestOutputHelper
/// </summary>
/// <typeparam name="T">日志分类类型（与被测试类一致）</typeparam>
public class TestOutputLogger<T>(ITestOutputHelper testOutput) : ILogger<T>
{
    private readonly ITestOutputHelper _TestOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
    private readonly string _CategoryName = typeof(T).FullName ?? nameof(TestOutputLogger<T>);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null; // 单元测试场景无需日志作用域
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None; // 启用所有非 None 级别日志
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        var logMessage = formatter(state, exception);
        var fullLogMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel.ToString().ToUpper()}] [{_CategoryName}] {logMessage}";

        _TestOutput.WriteLine(fullLogMessage);

        // 额外输出异常堆栈
        if (exception != null)
        {
            var exceptionMessage = $"【异常堆栈】{exception}";
            _TestOutput.WriteLine(exceptionMessage);
        }
    }
}

/// <summary>
/// 非泛型测试日志类：兼容非泛型 ILogger 接口
/// </summary>
public class TestOutputLogger : ILogger
{
    private readonly ITestOutputHelper _TestOutput;
    private readonly string _CategoryName;

    public TestOutputLogger(ITestOutputHelper testOutput, string categoryName)
    {
        _TestOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
        _CategoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        var logMessage = formatter(state, exception);
        var fullLogMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel.ToString().ToUpper()}] [{_CategoryName}] {logMessage}";

        _TestOutput.WriteLine(fullLogMessage);

        if (exception != null)
        {
            _TestOutput.WriteLine($"【异常堆栈】{exception}");
        }
    }
}