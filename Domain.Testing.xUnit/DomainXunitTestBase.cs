using TKW.Framework.Domain.Interfaces;
using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

// 1. 实现 IAsyncLifetime 接口
public abstract class DomainXunitTestBase<TUserInfo, TFixture> : IAsyncLifetime
    where TUserInfo : class, IUserInfo, new()
    where TFixture : DomainXunitTestFixtureBase<TUserInfo>
{
    protected readonly TFixture Fixture;
    protected TestOutputLoggerFactory TestLoggerFactory { get; }
    protected readonly ITestOutputHelper Output;

    // 2. 改为 public 属性，private set
    public DomainUser<TUserInfo>? User { get; private set; }
    private DomainSessionScope<TUserInfo>? _Scope;

    protected DomainXunitTestBase(TFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        XunitTestOutputBridge.Current = output;
        Output = output;
        TestLoggerFactory = new TestOutputLoggerFactory(new XunitTestWriter(output));
    }

    // 3. xUnit 会在构造函数执行完毕后，自动 await 这个方法
    public async ValueTask InitializeAsync()
    {
        // 在这里安全地异步开启会话
        _Scope = await DomainHost<TUserInfo>.Root!.CreateSessionScopeAsync();
        User = _Scope.User;

        // 调用提供给派生类的异步初始化钩子
        await OnInitializeAsync();
    }

    // 供派生类重写的初始化方法
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_Scope != null) await _Scope.DisposeAsync();
        // Fixture 的 Dispose 通常由 xUnit 的 Collection 机制自动管理，不需要在这里 Dispose Fixture
    }
}