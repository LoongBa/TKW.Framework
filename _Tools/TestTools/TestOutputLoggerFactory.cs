using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TKWF.Tools.TestTools;

/// <summary>
/// 测试专用 ILoggerFactory：批量创建 TestOutputLogger 实例
/// 支持注入到需要 ILoggerFactory 的被测试类中
/// </summary>
public class TestOutputLoggerFactory : ILoggerFactory
{
    private readonly ITestOutputHelper _TestOutput;
    // 缓存已创建的日志实例，避免重复创建
    private readonly ConcurrentDictionary<string, ILogger> _LoggerCache = new ConcurrentDictionary<string, ILogger>();
    private bool _Disposed;

    /// <summary>
    /// 构造函数：注入 ITestOutputHelper
    /// </summary>
    /// <param name="testOutput">xUnit 测试输出助手</param>
    public TestOutputLoggerFactory(ITestOutputHelper testOutput)
    {
        _TestOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
    }

    /// <summary>
    /// 创建指定分类名称的 ILogger 实例
    /// </summary>
    /// <param name="categoryName">日志分类名称（通常是类的全名）</param>
    /// <returns>TestOutputLogger 实例</returns>
    public ILogger CreateLogger(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            throw new ArgumentNullException(nameof(categoryName));
        }

        // 从缓存获取，不存在则创建，线程安全
        return _LoggerCache.GetOrAdd(categoryName, name => new TestOutputLogger(_TestOutput, name));
    }

    /// <summary>
    /// 便捷方法：创建泛型 ILogger<T> 实例
    /// </summary>
    /// <typeparam name="T">日志分类类型</typeparam>
    /// <returns>TestOutputLogger<T> 实例</returns>
    public ILogger<T> CreateLogger<T>()
    {
        var categoryName = typeof(T).FullName ?? nameof(T);
        // 先尝试从缓存获取，若不存在则创建泛型实例
        if (_LoggerCache.TryGetValue(categoryName, out var existingLogger))
        {
            return (ILogger<T>)existingLogger;
        }

        var newLogger = new TestOutputLogger<T>(_TestOutput);
        _LoggerCache.TryAdd(categoryName, newLogger);
        return newLogger;
    }

    /// <summary>
    /// 添加日志提供程序（测试场景无需实现，直接忽略）
    /// </summary>
    /// <param name="provider">日志提供程序</param>
    public void AddProvider(ILoggerProvider provider)
    {
        // 测试场景无需额外日志提供程序，空实现即可
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_Disposed)
        {
            return;
        }

        if (disposing)
        {
            // 清空日志缓存
            _LoggerCache.Clear();
        }

        _Disposed = true;
    }

    ~TestOutputLoggerFactory()
    {
        Dispose(false);
    }
}