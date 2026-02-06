using System.Collections.ObjectModel;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public sealed class DomainContext<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
    public DomainContext(
        DomainUser<TUserInfo> domainUser,
        IInvocation invocation,
        DomainContracts<TUserInfo> contracts,
        ILoggerFactory? loggerFactory = null)
    {
        DomainUser = domainUser;
        Invocation = new DomainMethodInvocation(invocation);
        MethodFilters = contracts.MethodFilters.AsReadOnly();
        ControllerFilters = contracts.ControllerFilters.AsReadOnly();
        MethodFlags = contracts.MethodFlags.AsReadOnly();
        ControllerFlags = contracts.ControllerFlags.AsReadOnly();
        Logger = loggerFactory?.CreateLogger($"{invocation.TargetType.Name}.{invocation.Method.Name}()");
    }
    public ILogger? Logger { get; }
    public DomainUser<TUserInfo> DomainUser { get; }
    public DomainMethodInvocation Invocation { get; }

    internal ReadOnlyCollection<DomainActionFilterAttribute<TUserInfo>> MethodFilters { get; }

    internal ReadOnlyCollection<DomainActionFilterAttribute<TUserInfo>> ControllerFilters { get; }
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; }

    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; }
}