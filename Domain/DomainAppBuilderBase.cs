using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public abstract class DomainAppBuilderBase<TSubBuilder, TOptions>(IDomainAppBuilderAdapter builder, TOptions options)
    where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions>
    where TOptions : DomainOptions
{
    protected readonly IDomainAppBuilderAdapter Builder = builder;
    protected readonly TOptions Options = options;

    // 对应原 DomainPipelineBuilderBase 的功能
    public TSubBuilder RegisterServices(Action<IServiceCollection, TOptions> action)
    {
        action(Builder.Services, Options);
        return (TSubBuilder)this;
    }

    // 核心构建器功能：配置 Options
    public TSubBuilder Configure(Action<TOptions> action)
    {
        action(Options);
        return (TSubBuilder)this;
    }

    // 核心构建器功能：配置 Autofac 容器 (解决 CS1660 编译错误)
    public TSubBuilder ConfigureContainer(Action<ContainerBuilder, TOptions> action)
    {
        Builder.ConfigureContainer(new AutofacServiceProviderFactory(), cb => action(cb, Options));
        return (TSubBuilder)this;
    }
    /// <summary>
    /// 使用指定的异常日志工厂实例
    /// </summary>
    public TSubBuilder UseExceptionLogger(DefaultExceptionLoggerFactory instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        // 利用 Autofac 容器的实例注册特性
        ConfigureContainer((cb, _) =>
        {
            // 将外部传入的实例注册为基类 DefaultExceptionLoggerFactory
            cb.RegisterInstance(instance)
                .As<DefaultExceptionLoggerFactory>()
                .SingleInstance();
        });
        return (TSubBuilder)this;
    }
    /// <summary>
    /// 使用指定的异常日志工厂实例
    /// </summary>
    public TSubBuilder UseExceptionLogger<TExceptionLogger>()
        where TExceptionLogger : DefaultExceptionLoggerFactory
    {
        // 利用 Autofac 容器的覆盖特性
        ConfigureContainer((cb, _) =>
        {
            // 无论表现层传入什么自定义派生类，都将其注册为基类 DefaultExceptionLoggerFactory
            // 这样领域层就能统一解析
            cb.RegisterType<TExceptionLogger>()
                .As<DefaultExceptionLoggerFactory>()
                .SingleInstance();
        });
        return (TSubBuilder)this;
    }
}