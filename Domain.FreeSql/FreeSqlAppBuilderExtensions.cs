using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.FreeSql;

public static class FreeSqlAppBuilderExtensions
{
    /// <summary>
    /// 配置 FreeSql 环境：包含单例注册和 FreeSqlEntityDAC 映射
    /// </summary>
    public static TSubBuilder SetFreeSqlEntityDAC<TSubBuilder, TOptions, TUserInfo>(
        this DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo> builder,
        IFreeSql? fsql = null) // 允许传入已构建好的实例，否则从 Options 读取
        where TSubBuilder : DomainAppBuilderBase<TSubBuilder, TOptions, TUserInfo>
        where TOptions : DomainOptions
        where TUserInfo : class, IUserInfo, new()
    {
        return builder.RegisterServices((services, options) =>
            {
                // 1. 注册 IFreeSql 单例
                // 如果外部没传，则尝试根据 ConnectionString 构建（保持灵活性）
                var instance = fsql ?? new FreeSqlBuilder()
                    .UseConnectionString(DataType.PostgreSQL, options.ConnectionString)
                    .UseAutoSyncStructure(options.IsDevelopment)
                    .Build();
                services.AddSingleton(instance);
                services.AddScoped<IDomainUnitOfWorkManager, FreeSqlUnitOfWorkManager>();
            })
            // 2. 调用刚才提取的通用方法注册 DAC
            .SetEntityDAC(typeof(FreeSqlEntityDAC<>));
    }
}