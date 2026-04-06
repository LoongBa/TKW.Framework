using Microsoft.Extensions.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Hosting;

public static class HostApplicationBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public TestAppBuilder<TUserInfo, TInitializer> ConfigTestAppDomain<TUserInfo, TInitializer>(Action<DomainOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
        {
            var options = new DomainOptions
            {
                IsDevelopment = builder.Environment.IsDevelopment(),
            };
            configure?.Invoke(options);
            return new TestAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), options);
        }

        public LocalAppBuilder<TUserInfo, TInitializer> ConfigConsoleAppDomain<TUserInfo, TInitializer>(Action<DomainOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
        {
            var options = new DomainOptions
            {
                IsDevelopment = builder.Environment.IsDevelopment(),
            };
            configure?.Invoke(options);
            return new LocalAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), options);
        }

        public LocalAppBuilder<TUserInfo, TInitializer> ConfigDesktopAppDomain<TUserInfo, TInitializer>(Action<DomainOptions>? configure = null)
            where TUserInfo : class, IUserInfo, new()
            where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
        {
            var options = new DomainOptions
            {
                IsDevelopment = builder.Environment.IsDevelopment(),
            };
            configure?.Invoke(options);
            return new LocalAppBuilder<TUserInfo, TInitializer>(new HostApplicationBuilderAdapter(builder), options);
        }
    }
}