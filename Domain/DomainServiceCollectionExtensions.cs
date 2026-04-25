using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public static class DomainServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Aop 服务（带装饰器代理）
    /// </summary>
    public static IServiceCollection AddAopService<TInterface, TImplementation, TDecorator, TUserInfo>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
        where TDecorator : class, TInterface
        where TUserInfo : class, IUserInfo, new()
    {
        // 注册 DomainUser 以便装饰器和实现类可以自动注入
        services.TryAddScoped<DomainUser<TUserInfo>>(sp =>
            DomainUser<TUserInfo>._ActiveScope.Value?.GetRequiredService<DomainUser<TUserInfo>>()
            ?? throw new InvalidOperationException("当前作用域未关联 DomainUser"));

        services.AddScoped<TImplementation>();
        services.AddScoped<TInterface>(sp =>
        {
            var impl = sp.GetRequiredService<TImplementation>();
            var interceptor = sp.GetRequiredService<StaticDomainInterceptor<TUserInfo>>();
            // 使用 ActivatorUtilities 避免装饰器的反射开销
            return ActivatorUtilities.CreateInstance<TDecorator>(sp, impl, interceptor);
        });
        return services;
    }

    /// <summary>
    /// 注册普通领域服务（不带 AOP）
    /// </summary>
    public static IServiceCollection AddService<TService, TImplementation, TUserInfo>(this IServiceCollection services)
        where TImplementation : class, TService
        where TService : class
        where TUserInfo : class, IUserInfo, new()
    {
        // 确保 DomainUser 可被注入
        services.TryAddScoped<DomainUser<TUserInfo>>(sp =>
             DomainUser<TUserInfo>._ActiveScope.Value?.GetService<DomainUser<TUserInfo>>()!);

        return services.AddScoped<TService, TImplementation>();
    }

    public static void UseLogger(this IServiceCollection services)
    {
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
    }

    public static void UseLogger(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        services.AddSingleton(loggerFactory);
    }
}