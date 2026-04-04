using System;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Testing;

public static class DomainTestBuilderExtensions
{
    public static DomainGenericHostBuilder<TUserInfo, TInitializer> ConfigTkwDomain<TUserInfo, TInitializer>(
        this IDomainAppBuilderAdapter builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainOptions();
        configure?.Invoke(options);

        // 返回构建器，并记录 Initializer 类型
        return new DomainGenericHostBuilder<TUserInfo, TInitializer>(builder, options);
    }
}