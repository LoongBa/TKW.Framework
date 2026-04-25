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
        services.TryAddScoped<TImplementation>();
        services.AddScoped<TInterface>(sp =>
        {
            // 解析原始实现实例
            var impl = sp.GetRequiredService<TImplementation>();
            // 获取全局 AOP 拦截器
            var interceptor = sp.GetRequiredService<StaticDomainInterceptor<TUserInfo>>();
            // 使用 ActivatorUtilities 避免装饰器的反射开销
            return ActivatorUtilities.CreateInstance<TDecorator>(sp, impl, interceptor);
        });
        return services;
    }

    /// <summary>
    /// 非泛型重载：注册 Aop 服务（带装饰器代理）
    /// </summary>
    public static IServiceCollection AddAopService(
        this IServiceCollection services,
        Type serviceInterface,
        Type implementation,
        Type proxyType,
        Type userInfoType)
    {
        // 1. 注册原始实现类 (AsSelf)，供装饰器构造函数调用
        services.TryAddScoped(implementation);

        // 2. 注册接口映射到代理类的工厂
        services.AddScoped(serviceInterface, sp =>
        {
            // A. 解析原始实现
            var impl = sp.GetRequiredService(implementation);

            // B. 动态构建拦截器类型：StaticDomainInterceptor<TUserInfo>
            // 因为 StaticDomainInterceptor 是在 Initialize 时注册的单例
            var interceptorType = typeof(StaticDomainInterceptor<>).MakeGenericType(userInfoType);
            var interceptor = sp.GetRequiredService(interceptorType);

            // C. 实例化生成的装饰器 (ProxyType)
            // ActivatorUtilities 会自动处理装饰器构造函数中除 impl 和 interceptor 之外的其他依赖（如 ILogger）
            return ActivatorUtilities.CreateInstance(sp, proxyType, impl, interceptor);
        });

        return services;
    }

    /// <summary>
    /// 注册普通领域服务（不带 AOP）
    /// </summary>
    public static IServiceCollection AddService<TService, TImplementation>(this IServiceCollection services)
        where TImplementation : class, TService
        where TService : class
    {
        // 直接作为类本身注册，不映射接口，等价于 AsSelf
        // 这确保了 User.Use<Implementation>() 能在 DI 链条中正确处理依赖
        return services.AddScoped<TService, TImplementation>();
    }

    /// <summary>
    /// 非泛型重载：注册普通领域服务（不带 AOP，AsSelf 模式）
    /// </summary>
    public static IServiceCollection AddService(this IServiceCollection services, Type implementation)
    {
        // 直接作为类本身注册，不映射接口
        // 确保了 User.Use<Implementation>() 能解析到单例化的 Scoped 实例
        services.TryAddScoped(implementation);
        return services;
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