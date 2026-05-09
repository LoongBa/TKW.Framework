using System;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Hosting;

namespace TKW.Framework.Domain.Interfaces;

public static class EntityDACExtensions
{
    /// <summary>
    /// 通用的 DAC 设置方法：注册指定的开放泛型实现
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="dacImplementationType">实现了 IEntityDAC 的开放泛型类型</param>
    public static TSubBuilder SetEntityDAC<TSubBuilder, TOptions, TUserInfo>(
        this DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo> builder,
        Type dacImplementationType)
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions
        where TUserInfo : class, IUserInfo, new()
    {
        // 校验是否为泛型类型定义
        if (!dacImplementationType.IsGenericTypeDefinition)
            throw new ArgumentException("IEntityDAC 实现类型必须是开放泛型（如 FreeSqlEntityDAC<>）", nameof(dacImplementationType));

        return builder.RegisterServices((services, _) =>
        {
            // 核心逻辑：将接口与指定的实现类绑定
            services.AddScoped(typeof(IEntityDAC<>), dacImplementationType);
            services.AddScoped(typeof(IEntityReadOnlyDAC<>), dacImplementationType);
        });
    }
}