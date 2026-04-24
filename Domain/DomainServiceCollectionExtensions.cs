using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public static class DomainServiceCollectionExtensions
{
    // 标准 Aop 服务注册
    public static IServiceCollection AddAopService<TInterface, TImplementation, TDecorator, TUserInfo>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
        where TDecorator : class, TInterface
        where TUserInfo : class, IUserInfo, new()
    {
        services.AddScoped<TImplementation>(); // 原始实现
        services.AddScoped<TInterface>(sp =>
        {
            var impl = sp.GetRequiredService<TImplementation>();
            var interceptor = sp.GetRequiredService<StaticDomainInterceptor<TUserInfo>>();
            // 利用 SG 生成的构造函数完成静态注入
            return (TDecorator)Activator.CreateInstance(typeof(TDecorator), impl, interceptor)!;
        });
        return services;
    }

    // 普通服务注册
    public static IServiceCollection AddService<TService, TImplementation>(this IServiceCollection services)
        where TImplementation : class, TService
        where TService : class
    {
        return services.AddScoped<TService, TImplementation>();
    }

    /// <summary>
    /// 注册日志工厂（可替换）：如果表现层未注册，则使用默认 LoggerFactory
    /// </summary>
    public static void UseLogger(this IServiceCollection services)
    {
        // TryAdd 确保了“可替换性”：如果容器中已存在 ILoggerFactory，则此注册无效
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
    }

    /// <summary>
    /// 注册指定的日志工厂实例
    /// </summary>
    public static void UseLogger(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        services.AddSingleton(loggerFactory);
    }
}