using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using TKW.Framework.Domain.Exceptions;
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
        // 守卫：确保实现类是可实例化的具体类
        if (typeof(TImplementation).IsInterface || typeof(TImplementation).IsAbstract)
            throw new DomainException($"[注册守卫] 实现类 {typeof(TImplementation).Name} 不能是接口或抽象类。");

        // 1. 注册原始实现类 (AsSelf)，供装饰器构造函数调用
        services.TryAddScoped<TImplementation>();

        // 2. 注册接口映射到装饰器工厂
        services.AddScoped<TInterface>(sp =>
        {
            var impl = sp.GetRequiredService<TImplementation>();
            var interceptor = sp.GetRequiredService<StaticDomainInterceptor<TUserInfo>>();

            // 使用 ActivatorUtilities 实例化生成的装饰器 (TDecorator)
            return ActivatorUtilities.CreateInstance<TDecorator>(sp, impl, interceptor);
        });

        return services;
    }

    /// <summary>
    /// 非泛型重载：注册 Aop 服务（带装饰器代理）
    /// </summary>
    public static IServiceCollection AddAopService(
        this IServiceCollection services, Type serviceInterface, 
        Type implementation, Type proxyType, Type userInfoType)
    {
        // 守卫 1：确保实现类是真正的类
        if (implementation.IsInterface || implementation.IsAbstract)
            throw new DomainException($"实现类 {implementation.Name} 不能是接口或抽象类。");

        // 守卫 2：确保装饰器确实实现了指定的契约接口
        if (!serviceInterface.IsAssignableFrom(proxyType))
            throw new DomainException($"生成的装饰器 {proxyType.Name} 未实现契约接口 {serviceInterface.Name}。");

        // 1. 注册原始实现类 (AsSelf)
        services.TryAddScoped(implementation);

        // 2. 注册接口映射到代理工厂
        services.AddScoped(serviceInterface, sp =>
        {
            var impl = sp.GetRequiredService(implementation);
            var interceptorType = typeof(StaticDomainInterceptor<>).MakeGenericType(userInfoType);
            var interceptor = sp.GetRequiredService(interceptorType);

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
        // 守卫：严禁将控制器（AOP契约类）作为普通 Service 注册
        if (typeof(IAopContract).IsAssignableFrom(typeof(TImplementation)))
            throw new DomainException($"[注册守卫] 类型 {typeof(TImplementation).Name} 属于 AOP 契约类，请改用 AddAopService 注册。");

        // 确保实现类自身也被注册 (AsSelf)，方便 User.Use<T>() 解析
        services.TryAddScoped<TImplementation>();

        // 注册映射关系
        if (typeof(TService) != typeof(TImplementation)) 
            services.AddScoped<TService, TImplementation>();

        return services;
    }

    /// <summary>
    /// 非泛型重载：注册普通领域服务（不带 AOP，AsSelf 模式）
    /// </summary>
    public static IServiceCollection AddService(this IServiceCollection services, Type implementation)
    {
        // 架构守卫：不允许将控制器作为普通 Service 注册
        if (typeof(IAopContract).IsAssignableFrom(implementation))
            throw new DomainException($"[注册守卫] 类型 {implementation.Name} 属于控制器契约类，必须通过 AddAopService 注册以启用拦截器。");
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