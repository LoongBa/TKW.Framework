using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域服务
/// </summary>
public abstract class DomainServiceBase : IDomainService
{
    protected internal DomainUser User { get; }
    protected TService Use<TService>() where TService : DomainServiceBase
    {
        return User.Use<TService>();
    }
    /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
    protected DomainServiceBase(DomainUser user)
    {
        User = user.EnsureNotNull(nameof(user));
    }
}

/// <summary>
/// 领域控制器：可选，封装 DomainService + IAopContract
/// </summary>
public class DomainController(DomainUser user) : DomainServiceBase(user), IAopContract;