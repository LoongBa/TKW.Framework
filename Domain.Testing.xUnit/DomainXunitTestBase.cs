using TKW.Framework.Domain.Interfaces;
using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

public abstract class DomainXunitTestBase<TUserInfo,TFixture>
    where TUserInfo : class, IUserInfo, new()
    where TFixture : DomainXunitTestFixtureBase<TUserInfo>
{
    protected readonly TFixture Fixture;
    protected TestOutputLoggerFactory TestLoggerFactory { get; }
    protected readonly ITestOutputHelper Output;


    protected DomainXunitTestBase(TFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        XunitTestOutputBridge.Current = output;
        // 方便派生类使用
        Output = output;
        TestLoggerFactory = new TestOutputLoggerFactory(new XunitTestWriter(output));
    }
}