using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

public class XunitTestWriter: XunitTestOutputBridge
{
    public XunitTestWriter(ITestOutputHelper output)
    {
        CurrentOutput.Value = output;
    }
}