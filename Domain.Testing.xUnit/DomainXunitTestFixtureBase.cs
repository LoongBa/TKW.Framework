using TKW.Framework.Domain.Interfaces;
using Xunit;

namespace TKW.Framework.Domain.Testing.xUnit;

public abstract class DomainXunitTestFixtureBase<TUserInfo> : IAsyncLifetime
    where TUserInfo : class, IUserInfo, new()
{
    public abstract ValueTask InitializeAsync();
    public ValueTask DisposeAsync() => new(Task.CompletedTask);
}