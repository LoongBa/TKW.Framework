using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public record DomainContext<TUserInfo>(DomainUser<TUserInfo> DomainUser, 
    DomainMethodInvocation Invocation, DomainContracts<TUserInfo> Contracts, ILoggerFactory LoggerFactory)
    where TUserInfo : class, IUserInfo, new()
{
    public ILogger Logger { get; } = LoggerFactory.CreateLogger($"{Invocation.TargetType.Name}.{Invocation.Method.Name}()");

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> MethodFilters { get; } = Contracts.MethodFilters.AsReadOnly();
    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; } = Contracts.ControllerFilters.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; } = Contracts.MethodFlags.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; } = Contracts.ControllerFlags.AsReadOnly();
}