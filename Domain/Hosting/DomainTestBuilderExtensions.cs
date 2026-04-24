using System;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class DomainTestBuilderExtensions
{
    public static TestAppBuilder<TUserInfo, TInitializer> ConfigTestDomain<TUserInfo, TInitializer>(
        this HostApplicationBuilderAdapter<TUserInfo> builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        var options = new DomainOptions();
        configure?.Invoke(options);

        return new TestAppBuilder<TUserInfo, TInitializer>(builder, options);
    }
}