using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TKW.Framework.Domain;

public abstract class DomainHostBuilder<TSubBuilder, TOptions>(IHostApplicationBuilder builder, TOptions options)
    where TSubBuilder : DomainHostBuilder<TSubBuilder, TOptions>
    where TOptions : DomainOptions
{
    protected readonly IHostApplicationBuilder Builder = builder;
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
}