using System.Collections.ObjectModel;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace TKW.Framework.Domain.Interception;

public sealed class DomainContext
{
    /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
    public DomainContext(
        DomainUser domainUser,
        IInvocation invocation,
        DomainContracts contracts,
        ILoggerFactory loggerFactory = null)
    {
        DomainUser = domainUser;
        Invocation = new DomainMethodInvocation(invocation);
        MethodFilters = contracts.MethodFilters.AsReadOnly();
        ControllerFilters = contracts.ControllerFilters.AsReadOnly();
        MethodFlags = contracts.MethodFlags.AsReadOnly();
        ControllerFlags = contracts.ControllerFlags.AsReadOnly();
        Logger = loggerFactory?.CreateLogger($"{invocation.TargetType.Name}.{invocation.Method.Name}()");
    }
    public ILogger Logger { get; }
    public DomainUser DomainUser { get; }
    public DomainMethodInvocation Invocation { get; }

    internal ReadOnlyCollection<DomainActionFilterAttribute> MethodFilters { get; }

    internal ReadOnlyCollection<DomainActionFilterAttribute> ControllerFilters { get; }
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; }

    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; }
}