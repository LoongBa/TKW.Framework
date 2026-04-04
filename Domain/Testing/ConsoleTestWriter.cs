using System;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Testing;

/// <summary>
/// 默认测试输出器：当用户未指定特定测试框架（如 xUnit）时，作为兜底方案
/// </summary>
public class ConsoleTestWriter : ITestWriter
{
    public void Write(string message) => Console.Write(message);
    public void WriteLine(string message) => Console.WriteLine(message);
}