using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Web;

public class DomainConfigurationBinder(WebApplicationBuilder builder, DomainWebOptions domainOptions)
{
    /// <summary>
    /// 绑定当前领域的主配置项
    /// </summary>
    public OptionsBuilder<DomainWebOptions> DomainOptions(string sectionName = "TKWDomain", bool reloadOnChange = true)
    {
        return BusinessOptions<DomainWebOptions>(sectionName, reloadOnChange);
    }

    /// <summary>
    /// 绑定额外的配置项 (不属于当前领域主 POCO)
    /// </summary>
    public OptionsBuilder<TDomainOptions> BusinessOptions<TDomainOptions>
        (string sectionName, bool reloadOnChange = true) where TDomainOptions : class
    {
        var section = builder.EnsureNotNull()!.Configuration.GetSection(sectionName);

        if (!section.Exists())
            throw new InvalidOperationException($"未找到配置节: {sectionName}");

        // 1. 如果泛型 TDomainOptions 正是当前实例 (或其基类)，立即绑定到 this
        // 这样在 ConfigTkwDomain 的闭包里，cfg 就能立刻读到值
        if (domainOptions is TDomainOptions currentInstance)
            section.Bind(currentInstance);

        // 2. 注册到 DI 体系
        var optionsBuilder = builder!.Services.AddOptions<TDomainOptions>()
            .Bind(section, binderOptions =>
            {
                // 这里可以配置绑定行为，例如是否允许枚举忽略大小写
            });

        // 3. 验证把关
        optionsBuilder
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return optionsBuilder;
    }
}