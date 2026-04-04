using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Testing;

public class NullTestWriter : ITestWriter
{
    public void Write(string message) { }
    public void WriteLine(string message) { }
}