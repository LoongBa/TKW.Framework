using System;
using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Testing.Hosting;

namespace TKW.Framework.Domain.Hosting;

public static class HostApplicationBuilderExtensions
{
    public static TestAppBuilder<TUserInfo, TInitializer> ConfigTestAppDomain<TUserInfo, TInitializer>(
        this IHostApplicationBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        return new TestAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), new DomainOptions());
    }
    public static LocalAppBuilder<TUserInfo, TInitializer> ConfigConsoleAppDomain<TUserInfo, TInitializer>(
        this IHostApplicationBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        return new LocalAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), new DomainOptions());
    }
    public static LocalAppBuilder<TUserInfo, TInitializer> ConfigDesktopAppDomain<TUserInfo, TInitializer>(
        this IHostApplicationBuilder builder,
        Action<DomainOptions>? configure = null)
        where TUserInfo : class, IUserInfo, new()
        where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
    {
        return new LocalAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), new DomainOptions());
    }
}