using TKW.Framework.Domain.Interfaces;
using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

public class XunitTestOutputBridge : ITestWriter
{
    protected static readonly AsyncLocal<ITestOutputHelper?> CurrentOutput = new();

    // 供基类设置
    public static ITestOutputHelper? Current
    {
        get => CurrentOutput.Value;
        set => CurrentOutput.Value = value;
    }

    public void WriteLine(string message) => Current?.WriteLine(message);
    public void Write(string message) => Current?.WriteLine(message);
}