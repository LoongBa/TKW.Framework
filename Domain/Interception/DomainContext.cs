using System.Collections.ObjectModel;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/*public record DomainContext<TUserInfo>(DomainUser<TUserInfo> DomainUser, 
    DomainMethodInvocation Invocation, DomainContracts<TUserInfo> Contracts, ILoggerFactory LoggerFactory)
    where TUserInfo : class, IUserInfo, new()
{
    public ILogger Logger { get; } = LoggerFactory.CreateLogger($"{Invocation.TargetType.Name}.{Invocation.Method.Name}()");

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> MethodFilters { get; } = Contracts.MethodFilters.AsReadOnly();
    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; } = Contracts.ControllerFilters.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; } = Contracts.MethodFlags.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; } = Contracts.ControllerFlags.AsReadOnly();
}*/

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

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> MethodFilters { get; }

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; }
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; }

    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; }
}