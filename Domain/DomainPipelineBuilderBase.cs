using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域管道构建器基类，处理跨平台通用的服务注册逻辑
/// </summary>
/// <typeparam name="TSubBuilder">子类类型，用于实现流式 API 的类型继承</typeparam>
public abstract class DomainPipelineBuilderBase<TSubBuilder>(IHostApplicationBuilder builder, DomainOptions options)
{
    protected readonly IHostApplicationBuilder Builder = builder;
    protected readonly DomainOptions Options = options;

    /// <summary>
    /// 注册业务自定义服务。此阶段属于 DI 容器构建期。
    /// </summary>
    public TSubBuilder RegisterServices(Action<IServiceCollection, DomainOptions> action)
    {
        action(Builder.Services, Options);
        return (TSubBuilder)(object)this;
    }
}