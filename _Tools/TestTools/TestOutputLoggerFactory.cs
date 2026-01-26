using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TKWF.Tools.TestTools;

/// <summary>
/// xUnit 单元测试专用 <see cref="ILoggerFactory"/> 实现类（实例化版本，支持多实例独立配置）
/// 核心功能：
/// 1.  批量创建 <see cref="TestOutputLogger"/> 实例，将日志输出到 xUnit 测试控制台
/// 2.  支持实例级日志级别过滤，多实例配置互不干扰
/// 3.  支持日志条目存储，便于单元测试断言验证
/// 4.  日志实例缓存，避免重复创建相同分类的日志对象
/// 5.  兼容泛型 <see cref="ILogger{T}"/> 和非泛型 <see cref="ILogger"/> 创建
/// </summary>
public class TestOutputLoggerFactory : ILoggerFactory
{
    /// <summary>
    /// xUnit 测试输出助手，用于将日志输出到测试控制台/测试报告
    /// </summary>
    private readonly ITestOutputHelper _TestOutput;

    /// <summary>
    /// 日志实例缓存字典（线程安全）
    /// Key：日志分类名称（通常是被日志记录类的完整类型名）
    /// Value：对应分类的 <see cref="ILogger"/> 实例
    /// </summary>
    private readonly ConcurrentDictionary<string, ILogger> _LoggerCache = new();

    /// <summary>
    /// 资源释放标记：防止重复释放资源
    /// </summary>
    private bool _Disposed;

    #region 实例级日志配置（独立控制，多实例互不干扰）
    /// <summary>
    /// 实例级日志级别过滤委托（默认输出所有级别日志）
    /// 可自定义过滤规则，例如：仅输出 Debug 和 Error 级别
    /// </summary>
    public Func<LogLevel, bool> LogLevelFilter { get; set; } = level => true;

    /// <summary>
    /// 实例级日志条目存储（仅当前实例输出的日志，便于单元测试断言）
    /// </summary>
    public List<TestLogEntry> LogEntries { get; } = [];
    #endregion

    #region 构造函数
    /// <summary>
    /// 构造函数：注入 xUnit 测试输出助手，初始化日志工厂
    /// </summary>
    /// <param name="testOutput">xUnit 测试输出助手（由 xUnit 自动注入到测试类中）</param>
    /// <exception cref="ArgumentNullException">当 testOutput 为 null 时抛出</exception>
    public TestOutputLoggerFactory(ITestOutputHelper testOutput)
    {
        // 校验入参非空，避免后续日志输出时出现空引用异常
        _TestOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
    }
    #endregion

    #region 日志实例创建（兼容泛型/非泛型，带缓存）
    /// <summary>
    /// 创建指定分类名称的非泛型 <see cref="ILogger"/> 实例
    /// 优先从缓存获取，缓存不存在则创建新实例并加入缓存
    /// </summary>
    /// <param name="categoryName">日志分类名称（推荐使用被日志记录类的完整类型名）</param>
    /// <returns>对应分类的 <see cref="TestOutputLogger"/> 实例</returns>
    /// <exception cref="ArgumentNullException">当 categoryName 为 null 或空字符串时抛出</exception>
    public ILogger CreateLogger(string categoryName)
    {
        // 校验分类名称非空，确保日志分类有效性
        if (string.IsNullOrEmpty(categoryName))
        {
            throw new ArgumentNullException(nameof(categoryName), "日志分类名称不能为空");
        }

        // 线程安全的缓存获取/创建：绑定当前实例的配置（过滤规则+日志存储）
        return _LoggerCache.GetOrAdd(
            key: categoryName,
            valueFactory: name => new TestOutputLogger(_TestOutput, name, this)
        );
    }

    /// <summary>
    /// 便捷方法：创建泛型 <see cref="ILogger{T}"/> 实例
    /// 泛型 T 即为日志分类类型，简化泛型日志的创建流程
    /// </summary>
    /// <typeparam name="T">日志分类类型（通常是被日志记录的业务类）</typeparam>
    /// <returns>对应泛型类型的 <see cref="TestOutputLogger{T}"/> 实例</returns>
    public ILogger<T> CreateLogger<T>()
    {
        // 获取泛型类型的完整名称作为日志分类名称（优先使用 FullName，不存在则使用类型名）
        var categoryName = typeof(T).FullName ?? nameof(T);

        // 先尝试从缓存获取已存在的日志实例，避免重复创建
        if (_LoggerCache.TryGetValue(categoryName, out var existingLogger))
        {
            // 转换为泛型 ILogger<T> 实例并返回
            return (ILogger<T>)existingLogger;
        }

        // 缓存不存在时，创建泛型 TestOutputLogger<T> 实例（绑定当前工厂实例配置）
        var newLogger = new TestOutputLogger<T>(_TestOutput, this);
        // 尝试将新实例加入缓存（TryAdd 保证线程安全，避免并发场景下的重复添加）
        _LoggerCache.TryAdd(categoryName, newLogger);

        return newLogger;
    }
    #endregion

    #region 实例级日志配置快捷方法（简化使用）
    /// <summary>
    /// 快速配置：仅输出 ≥ 指定级别的日志
    /// 例如：SetMinimumLogLevel(LogLevel.Information) 仅输出 Info/Warn/Error/Critical
    /// </summary>
    /// <param name="minLevel">最小日志级别（低于该级别的日志将被过滤）</param>
    public void SetMinimumLogLevel(LogLevel minLevel)
    {
        LogLevelFilter = level => level >= minLevel;
        ClearLogEntries(); // 重置当前实例的日志存储，避免配置变更前的日志干扰
    }

    /// <summary>
    /// 快速配置：关闭当前实例的所有日志输出
    /// </summary>
    public void DisableAllLogging()
    {
        LogLevelFilter = level => false;
        ClearLogEntries(); // 重置日志存储
    }

    /// <summary>
    /// 重置当前实例的日志配置（恢复默认：输出所有日志）
    /// </summary>
    public void ResetLogConfiguration()
    {
        LogLevelFilter = level => true;
        ClearLogEntries(); // 清空历史日志
    }

    /// <summary>
    /// 清空当前实例的日志条目存储（线程安全）
    /// </summary>
    public void ClearLogEntries()
    {
        lock (LogEntries) // 加锁保证线程安全，避免并行测试时的集合操作异常
        {
            LogEntries.Clear();
        }
    }
    #endregion

    #region 接口强制实现（测试场景无需扩展）
    /// <summary>
    /// 添加自定义日志提供程序（测试场景无需实现，空方法占位）
    /// 该方法为 <see cref="ILoggerFactory"/> 接口的强制实现，本类已内置 TestOutputLogger 支持
    /// </summary>
    /// <param name="provider">日志提供程序实例（本方法忽略该参数）</param>
    public void AddProvider(ILoggerProvider provider)
    {
        // 测试场景下，仅需将日志输出到 xUnit 测试控制台，无需添加其他第三方日志提供程序
        // 空实现以满足接口契约要求
    }
    #endregion

    #region IDisposable 实现（资源释放）
    /// <summary>
    /// 释放当前日志工厂占用的所有资源（缓存的日志实例、非托管资源等）
    /// </summary>
    public void Dispose()
    {
        // 调用受保护的释放方法，标记为托管资源释放
        Dispose(disposing: true);
        // 通知 GC 无需再调用该对象的终结器
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 受保护的资源释放方法（实现 IDisposable 模式的核心方法）
    /// 支持托管资源和非托管资源的分别释放
    /// </summary>
    /// <param name="disposing">是否释放托管资源：true=释放托管+非托管；false=仅释放非托管</param>
    protected virtual void Dispose(bool disposing)
    {
        // 若已释放，则直接返回，避免重复释放
        if (_Disposed)
        {
            return;
        }

        // 释放托管资源（仅当 disposing 为 true 时执行）
        if (disposing)
        {
            // 清空日志实例缓存，释放所有缓存的 ILogger 实例引用
            _LoggerCache.Clear();
            // 清空日志条目存储
            ClearLogEntries();
        }

        // 标记资源已释放（非托管资源释放可在此处扩展，当前无额外非托管资源）
        _Disposed = true;
    }

    /// <summary>
    /// 终结器（析构函数）：用于释放非托管资源
    /// 当对象未被手动调用 Dispose() 时，由 GC 自动调用
    /// </summary>
    ~TestOutputLoggerFactory()
    {
        // 仅释放非托管资源，托管资源由 GC 自动回收
        Dispose(disposing: false);
    }
    #endregion

    #region 内部辅助类：测试日志条目（用于存储和断言）
    /// <summary>
    /// 测试日志条目实体（包含日志级别、分类、消息、异常等完整信息）
    /// 用于单元测试中断言日志是否正确输出
    /// </summary>
    public class TestLogEntry
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// 日志分类名称（对应被日志记录类的完整类型名）
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// 格式化后的日志消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 日志关联的异常信息（若有）
        /// </summary>
        public Exception? Exception { get; set; }
    }
    #endregion

    #region 内部辅助类：TestOutputLogger（绑定工厂实例配置）
    /// <summary>
    /// 非泛型测试日志类（绑定所属工厂实例的过滤规则和日志存储）
    /// </summary>
    private class TestOutputLogger(ITestOutputHelper testOutput, string categoryName, TestOutputLoggerFactory factory)
        : ILogger
    {
        // 绑定所属工厂实例

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null; // 测试场景无需日志作用域，返回空即可
        }

        /// <summary>
        /// 核心：根据所属工厂的过滤规则，判断当前日志级别是否启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>是否启用该级别日志</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None) return false;
            return factory.LogLevelFilter.Invoke(logLevel); // 调用实例级过滤规则
        }

        /// <summary>
        /// 核心：输出日志（同时存储到工厂实例的日志条目集合，输出到 xUnit 控制台）
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 先判断当前级别是否启用，未启用则直接返回
            if (!IsEnabled(logLevel)) return;

            // 格式化日志消息
            var logMessage = formatter(state, exception);

            // 1. 存储日志条目到所属工厂实例（线程安全）
            lock (factory.LogEntries)
            {
                factory.LogEntries.Add(new TestLogEntry
                {
                    LogLevel = logLevel,
                    CategoryName = categoryName,
                    Message = logMessage,
                    Exception = exception
                });
            }

            // 2. 输出日志到 xUnit 测试控制台（便于调试查看）
            var formattedOutput = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel,-10}] [{categoryName}] {logMessage}";
            if (exception != null)
            {
                formattedOutput += $"{Environment.NewLine}异常详情：{exception}";
            }
            testOutput.WriteLine(formattedOutput);
        }
    }

    /// <summary>
    /// 泛型测试日志类（绑定所属工厂实例配置）
    /// </summary>
    /// <typeparam name="T">日志分类类型</typeparam>
    private class TestOutputLogger<T> : ILogger<T>
    {
        private readonly TestOutputLogger _Logger;

        public TestOutputLogger(ITestOutputHelper testOutput, TestOutputLoggerFactory factory)
        {
            var categoryName = typeof(T).FullName ?? nameof(T);
            _Logger = new TestOutputLogger(testOutput, categoryName, factory);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _Logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _Logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _Logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
    #endregion
}