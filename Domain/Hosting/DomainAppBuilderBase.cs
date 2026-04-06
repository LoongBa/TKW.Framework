using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Hosting;

public abstract class DomainAppBuilderBase<TSubBuilder, TOptions>(IDomainAppBuilderAdapter builder, TOptions options)
    where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions>
    where TOptions : DomainOptions
{
    protected readonly IDomainAppBuilderAdapter Builder = builder;
    protected readonly TOptions Options = options;

    // 【魔法核心】：动作缓冲池
    private readonly List<Action<ContainerBuilder, TOptions>> _containerActions = new();
    private bool _isFactoryRegistered = false;

    public TSubBuilder RegisterServices(Action<IServiceCollection, TOptions> action)
    {
        action(Builder.Services, Options);
        return (TSubBuilder)this;
    }

    public TSubBuilder Configure(Action<TOptions> action)
    {
        action(Options);
        return (TSubBuilder)this;
    }

    // 核心构建器功能：配置 Autofac 容器 
    public TSubBuilder ConfigureContainer(Action<ContainerBuilder, TOptions> action)
    {
        // 1. 将注册动作放入缓冲池
        _containerActions.Add(action);

        // 2. 只向底层注册一次 Factory。
        if (!_isFactoryRegistered)
        {
            Builder.ConfigureContainer(new AutofacServiceProviderFactory(), cb =>
            {
                // 当 .NET 底层最终调用 Build() 触发此委托时，
                // _containerActions 已经被后续所有的链式调用填满了！
                foreach (var queuedAction in _containerActions)
                {
                    queuedAction(cb, Options);
                }
            });
            _isFactoryRegistered = true;
        }

        return (TSubBuilder)this;
    }

    protected TSubBuilder UseSessionManagerInternal<TUserInfo, TSessionManager>()
        where TUserInfo : class, IUserInfo, new()
        where TSessionManager : ISessionManager<TUserInfo>
    {
        ConfigureContainer((cb, _) =>
        {
            cb.RegisterType<TSessionManager>().As<ISessionManager<TUserInfo>>().SingleInstance();
        });
        return (TSubBuilder)this;
    }

    protected TSubBuilder UseSessionManagerInternal<TUserInfo>(ISessionManager<TUserInfo> instance)
        where TUserInfo : class, IUserInfo, new()
    {
        ArgumentNullException.ThrowIfNull(instance);
        ConfigureContainer((cb, _) =>
        {
            cb.RegisterInstance(instance).As<ISessionManager<TUserInfo>>().SingleInstance();
        });
        return (TSubBuilder)this;
    }

    public TSubBuilder UseExceptionLogger(DefaultExceptionLoggerFactory instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ConfigureContainer((cb, _) =>
        {
            cb.RegisterInstance(instance).As<DefaultExceptionLoggerFactory>().SingleInstance();
        });
        return (TSubBuilder)this;
    }

    public TSubBuilder UseExceptionLogger<TExceptionLogger>()
        where TExceptionLogger : DefaultExceptionLoggerFactory
    {
        ConfigureContainer((cb, _) =>
        {
            cb.RegisterType<TExceptionLogger>().As<DefaultExceptionLoggerFactory>().SingleInstance();
        });
        return (TSubBuilder)this;
    }
}