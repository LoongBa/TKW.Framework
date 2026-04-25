using TKW.Framework.Domain.Interfaces;
using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

public abstract class DomainXunitTestBase<TUserInfo, TFixture> : IAsyncDisposable
    where TUserInfo : class, IUserInfo, new()
    where TFixture : DomainXunitTestFixtureBase<TUserInfo>
{
    protected readonly TFixture Fixture;
    protected TestOutputLoggerFactory TestLoggerFactory { get; }
    protected readonly ITestOutputHelper Output;

    public DomainUser<TUserInfo> User { get; }
    private readonly DomainSessionScope<TUserInfo> _Scope;


    protected DomainXunitTestBase(TFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        XunitTestOutputBridge.Current = output;
        // 方便派生类使用
        Output = output;
        TestLoggerFactory = new TestOutputLoggerFactory(new XunitTestWriter(output));
        _Scope = DomainHost<TUserInfo>.Root!.CreateSessionScopeAsync().Result;
        User = _Scope.User;
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        await _Scope.DisposeAsync();
    }
}