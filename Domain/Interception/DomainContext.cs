using System.Collections.ObjectModel;
using Autofac;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public sealed class DomainContext<TUserInfo>(
    DomainUser<TUserInfo> domainUser,
    IInvocation invocation,
    DomainContracts<TUserInfo> contracts,
    ILifetimeScope lifetimeScope,
    ILoggerFactory? loggerFactory = null)
    where TUserInfo : class, IUserInfo, new()
{
    public ILifetimeScope LifetimeScope { get; } = lifetimeScope; // 保存当前 AOP 作用域
    // 暴露作用域
    public ILogger? Logger { get; } = loggerFactory?.CreateLogger($"{invocation.TargetType.Name}.{invocation.Method.Name}()");
    public DomainUser<TUserInfo> DomainUser { get; } = domainUser;
    public DomainMethodInvocation Invocation { get; } = new(invocation);

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> MethodFilters { get; } = contracts.MethodFilters.AsReadOnly();
    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; } = contracts.ControllerFilters.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; } = contracts.MethodFlags.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; } = contracts.ControllerFlags.AsReadOnly();
}