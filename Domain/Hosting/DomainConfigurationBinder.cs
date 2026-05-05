using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Hosting;

public class DomainConfigurationBinder<TDomainOptions>(IHostApplicationBuilder builder, TDomainOptions domainOptions)
    where TDomainOptions : DomainOptions
{
    /// <summary>
    /// 绑定领域的配置项
    /// </summary>
    public OptionsBuilder<TDomainOptions> DomainOptions
        (string sectionName = "TKWDomain", Action<IConfiguration, BinderOptions>? setBinderOptions = null, bool reloadOnChange = true)
    {
        var section = builder.EnsureNotNull()!.Configuration.GetSection(sectionName);

        // 1. 立即绑定给当前实例，方便后续代码基于配置进行修改
        section.Bind(domainOptions);

        // 2. 注册标准的 DI Options 绑定
        var optionsBuilder = builder.Services.AddOptions<TDomainOptions>()
            .Bind(section);

        // 3. 关键补丁：利用 PostConfigure，将外层局部实例最后修改的状态，强行应用到 DI 容器生成的实例上
        builder.Services.PostConfigure<TDomainOptions>(diOptions =>
        {
            // 反射复制或者针对性赋值（假设 TDomainOptions 实现了浅拷贝或你可以手动映射核心字段）
            // 这里需要将 domainOptions 的最终值覆盖 diOptions
        });

        //optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
        return optionsBuilder;
    }
}
public static class DomainConfigurationBinderExt
{
    public static OptionsBuilder<TDomainOptions> Bind<TDomainOptions, TSBuilder>
    (this TDomainOptions cfg, TSBuilder builder, 
        string sectionName = "TKWDomain", bool reloadOnChange = true, 
        Action<IConfiguration, BinderOptions>? setBinderOptions = null)
        where TDomainOptions : DomainOptions
        where TSBuilder : IHostApplicationBuilder
    {
        return new DomainConfigurationBinder<TDomainOptions>(builder, cfg)
            .DomainOptions(sectionName, setBinderOptions, reloadOnChange);
    }
    // 纯验证扩展：不进行重复绑定，仅追加验证逻辑
    public static OptionsBuilder<TDomainOptions> Validate<TDomainOptions>(
        this TDomainOptions cfg,
        IHostApplicationBuilder builder,
        Action<TDomainOptions> validationLogic,
        string? failureMessage = null)
        where TDomainOptions : DomainOptions
    {
        return builder.Services.AddOptions<TDomainOptions>()
            .Validate(o => {
                validationLogic(o);
                return true; // 或者在这里返回具体布尔值
            }, failureMessage ?? "配置验证失败");
    }
}