using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// V4 领域执行上下文：完全不依赖第三方 AOP 或 DI 框架
/// </summary>
public sealed class DomainContext<TUserInfo>(
    DomainUser<TUserInfo> domainUser,
    InvocationContext invocation,
    DomainContracts<TUserInfo> contracts,
    IServiceProvider serviceProvider, // 注入标准 IServiceProvider
    ILoggerFactory? loggerFactory = null)
    where TUserInfo : class, IUserInfo, new()
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    public ILogger? Logger { get; } = loggerFactory?.CreateLogger($"{invocation.Target.GetType().Name}.{invocation.MethodName}()");
    public DomainUser<TUserInfo> DomainUser { get; } = domainUser;
    public InvocationContext Invocation { get; } = invocation;

    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> MethodFilters { get; } = contracts.MethodFilters.AsReadOnly();
    internal ReadOnlyCollection<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; } = contracts.ControllerFilters.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> MethodFlags { get; } = contracts.MethodFlags.AsReadOnly();
    public ReadOnlyCollection<DomainFlagAttribute> ControllerFlags { get; } = contracts.ControllerFlags.AsReadOnly();
}